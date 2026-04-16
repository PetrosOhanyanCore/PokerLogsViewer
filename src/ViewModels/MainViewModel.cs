using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using PokerLogsViewer.Models;
using PokerLogsViewer.Services;

namespace PokerLogsViewer.ViewModels
{
    public sealed class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IFileScanner _fileScanner;
        private readonly IJsonParser _jsonParser;
        private readonly Dispatcher _dispatcher;

        private readonly object _watcherSync = new object();
        private FileSystemWatcher _watcher;
        private Timer _autoRefreshTimer;
        private string _watchedRootPath;
        private volatile bool _isDisposed;
        private int _isAutoRefreshRunning;

        private Thread _scanThread;
        // Volatile because it is read/written by both UI and worker threads.
        private volatile bool _isScanning;

        private string _folderPath;
        private string _status;
        private StatusKind _statusKind = StatusKind.Idle;
        private TableGroupViewModel _selectedTable;
        private PokerHand _selectedHand;
        private string _tableFilterText;
        private string _handFilterText;

        private readonly ICollectionView _filteredTables;
        private ICollectionView _filteredHands;

        private static readonly ObservableCollection<PokerHand> EmptyHands = new ObservableCollection<PokerHand>();

        public ObservableCollection<TableGroupViewModel> Tables { get; }
            = new ObservableCollection<TableGroupViewModel>();

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (SetProperty(ref _folderPath, value))
                    ((RelayCommand)ScanCommand).RaiseCanExecuteChanged();
            }
        }

        public string TableFilterText
        {
            get => _tableFilterText;
            set
            {
                if (!SetProperty(ref _tableFilterText, value)) return;
                _filteredTables.Refresh();
            }
        }

        public string HandFilterText
        {
            get => _handFilterText;
            set
            {
                if (!SetProperty(ref _handFilterText, value)) return;
                _filteredHands?.Refresh();
            }
        }

        public ICollectionView FilteredTables => _filteredTables;
        public ICollectionView FilteredHands => _filteredHands;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Drives the status-bar text color via DataTriggers in XAML.
        /// </summary>
        public StatusKind StatusKind
        {
            get => _statusKind;
            set => SetProperty(ref _statusKind, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (_isScanning == value) return;
                _isScanning = value;
                OnPropertyChanged();
                ((RelayCommand)ScanCommand).RaiseCanExecuteChanged();
            }
        }

        public TableGroupViewModel SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    SelectedHand = null; // reset detail panel
                    RebuildHandsView();
                }
            }
        }

        public PokerHand SelectedHand
        {
            get => _selectedHand;
            set => SetProperty(ref _selectedHand, value);
        }

        public ICommand BrowseCommand { get; }
        public ICommand ScanCommand { get; }

        public MainViewModel(IFileScanner fileScanner, IJsonParser jsonParser)
        {
            _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
            _jsonParser = jsonParser ?? throw new ArgumentNullException(nameof(jsonParser));
            _dispatcher = Application.Current.Dispatcher;

            BrowseCommand = new RelayCommand(_ => Browse(), _ => !IsScanning);
            ScanCommand = new RelayCommand(
                _ => StartScan(),
                _ => !IsScanning
                     && !string.IsNullOrWhiteSpace(FolderPath)
                     && Directory.Exists(FolderPath));

            _filteredTables = CollectionViewSource.GetDefaultView(Tables);
            _filteredTables.Filter = FilterTable;

            RebuildHandsView();

            // Initialize status text from localization
            // Use keyed status so it updates when language changes
            SetStatusKey("Status_SelectFolder");

            // Recompute status when localization changes
            LocalizationManager.Instance.PropertyChanged += (_, __) =>
            {
                // Re-evaluate current keyed status on UI thread
                _dispatcher.BeginInvoke(new Action(() => UpdateStatusFromKey()));
            };
        }

        private string _statusKey;
        private object[] _statusArgs;

        private void SetStatusKey(string key, params object[] args)
        {
            _statusKey = key;
            _statusArgs = args != null && args.Length == 0 ? null : args;
            UpdateStatusFromKey();
        }

        private void SetStatusText(string text)
        {
            _statusKey = null;
            _statusArgs = null;
            Status = text;
        }

        private void UpdateStatusFromKey()
        {
            if (_statusKey == null) return;
            var fmt = LocalizationManager.Instance[_statusKey];
            try
            {
                Status = _statusArgs != null ? string.Format(fmt, _statusArgs) : fmt;
            }
            catch (FormatException)
            {
                // Fallback to raw format if localization format is invalid
                Status = fmt;
            }
        }

        // ---------------------------------------------------------------------
        // UI-thread methods
        // ---------------------------------------------------------------------

        private void Browse()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the folder containing poker log JSON files",
                ShowNewFolderButton = false
            })
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    FolderPath = dlg.SelectedPath;
            }
        }

        private void StartScan()
        {
            if (IsScanning || _isDisposed) return;

            var path = FolderPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                SetStatusKey("Status_Error_FolderNotExist");
                StatusKind = StatusKind.Error;
                return;
            }

            ConfigureWatcher(path);

            // Reset UI state on UI thread before leaving.
            IsScanning = true;
            SetStatusKey("Status_Scanning");
            StatusKind = StatusKind.Scanning;
            Tables.Clear();
            SelectedTable = null;
            SelectedHand = null;

            _scanThread = new Thread(() => ScanWorker(path))
            {
                Name = "PokerLogsScanThread",
                IsBackground = true
            };
            _scanThread.Start();
        }

        // ---------------------------------------------------------------------
        // Background-thread work
        // ---------------------------------------------------------------------

        /// <summary>
        /// Runs on <see cref="_scanThread"/>. MUST NOT touch ObservableCollections
        /// or bindable properties directly — all UI mutations go through
        /// <see cref="_dispatcher"/>.
        /// </summary>
        private void ScanWorker(string rootPath)
        {
            try
            {
                var grouped = BuildGroupedHands(rootPath, reportProgress: true, out int processed, out int failed);

                // 3. Hand off to the UI thread in a single batched update.
                _dispatcher.Invoke(new Action(() =>
                {
                    ApplyTables(grouped);

                    if (failed > 0)
                        SetStatusKey("Status_Done_ProcessedSkipped", processed, failed);
                    else
                        SetStatusKey("Status_Done_Processed", processed);
                    StatusKind = StatusKind.Done;

                    IsScanning = false;
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScanWorker] fatal: {ex}");
                _dispatcher.Invoke(new Action(() =>
                {
                    SetStatusKey("Status_Error_Prefix", ex.Message);
                    StatusKind = StatusKind.Error;
                    IsScanning = false;
                }));
            }
        }

        private Dictionary<string, List<PokerHand>> BuildGroupedHands(string rootPath, bool reportProgress, out int processed, out int failed)
        {
            processed = 0;
            failed = 0;

            var files = _fileScanner.FindJsonFiles(rootPath).ToList();
            int total = files.Count;

            if (reportProgress)
                PostStatus(string.Format(LocalizationManager.Instance["Status_FoundFilesParsing"], total));

            var grouped = new Dictionary<string, List<PokerHand>>(StringComparer.Ordinal);

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                try
                {
                    var hands = _jsonParser.ParseFile(file);
                    if (hands == null)
                    {
                        failed++;
                        continue;
                    }

                    foreach (var hand in hands)
                    {
                        var key = string.IsNullOrWhiteSpace(hand.TableName)
                            ? "(Unknown)"
                            : hand.TableName;

                        if (!grouped.TryGetValue(key, out var list))
                        {
                            list = new List<PokerHand>();
                            grouped[key] = list;
                        }
                        list.Add(hand);
                    }
                    processed++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BuildGroupedHands] file '{file}': {ex.Message}");
                    failed++;
                }

                if (reportProgress && ((i & 0x1F) == 0))
                    PostStatus(string.Format(LocalizationManager.Instance["Status_ScanningProgress"], i + 1, total));
            }

            return grouped;
        }

        private void ApplyTables(Dictionary<string, List<PokerHand>> grouped)
        {
            Tables.Clear();
            foreach (var kv in grouped.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var group = new TableGroupViewModel(kv.Key);
                foreach (var h in kv.Value.OrderBy(h => h.HandID))
                    group.Hands.Add(h);
                Tables.Add(group);
            }

            _filteredTables.Refresh();
            _filteredHands?.Refresh();
        }

        private bool FilterTable(object item)
        {
            if (item is not TableGroupViewModel table)
                return false;

            if (string.IsNullOrWhiteSpace(TableFilterText))
                return true;

            return table.TableName?.IndexOf(TableFilterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool FilterHand(object item)
        {
            if (item is not PokerHand hand)
                return false;

            if (string.IsNullOrWhiteSpace(HandFilterText))
                return true;

            var handIdText = hand.HandID.ToString();
            return handIdText.IndexOf(HandFilterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RebuildHandsView()
        {
            var source = (IEnumerable<PokerHand>)(SelectedTable?.Hands ?? EmptyHands);
            _filteredHands = CollectionViewSource.GetDefaultView(source);
            _filteredHands.Filter = FilterHand;
            _filteredHands.Refresh();
            OnPropertyChanged(nameof(FilteredHands));
        }

        // ---------------------------------------------------------------------
        // Auto-refresh via FileSystemWatcher
        // ---------------------------------------------------------------------

        private void ConfigureWatcher(string rootPath)
        {
            lock (_watcherSync)
            {
                DisposeWatcherCore();

                _watchedRootPath = rootPath;

                _watcher = new FileSystemWatcher(rootPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime
                };

                _watcher.Changed += OnWatchedFileChanged;
                _watcher.Created += OnWatchedFileChanged;
                _watcher.Deleted += OnWatchedFileChanged;
                _watcher.Renamed += OnWatchedFileRenamed;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;

                _autoRefreshTimer = new Timer(_ => StartAutoRefresh(), null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_isDisposed) return;
            if (!IsAcceptedLogFile(e.FullPath)) return;
            ScheduleAutoRefresh();
        }

        private void OnWatchedFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_isDisposed) return;
            if (!IsAcceptedLogFile(e.FullPath) && !IsAcceptedLogFile(e.OldFullPath)) return;
            ScheduleAutoRefresh();
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            if (_isDisposed) return;

            Debug.WriteLine($"[Watcher] error: {e.GetException()?.Message}");
            var path = _watchedRootPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                ConfigureWatcher(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Watcher] reconfigure failed: {ex.Message}");
            }
        }

        private static bool IsAcceptedLogFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return false;
            return fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || fullPath.EndsWith(".json.txt", StringComparison.OrdinalIgnoreCase);
        }

        private void ScheduleAutoRefresh()
        {
            lock (_watcherSync)
            {
                _autoRefreshTimer?.Change(600, Timeout.Infinite);
            }
        }

        private void StartAutoRefresh()
        {
            if (_isDisposed) return;
            if (IsScanning) return;

            if (Interlocked.Exchange(ref _isAutoRefreshRunning, 1) == 1)
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    RunAutoRefresh();
                }
                finally
                {
                    Interlocked.Exchange(ref _isAutoRefreshRunning, 0);
                }
            });
        }

        private void RunAutoRefresh()
        {
            var path = _watchedRootPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                var grouped = BuildGroupedHands(path, reportProgress: false, out int processed, out int failed);

                _dispatcher.Invoke(() =>
                {
                    var previousTable = SelectedTable?.TableName;
                    long? previousHandId = SelectedHand?.HandID;

                    ApplyTables(grouped);

                    if (!string.IsNullOrWhiteSpace(previousTable))
                    {
                        SelectedTable = Tables.FirstOrDefault(t => string.Equals(t.TableName, previousTable, StringComparison.Ordinal));
                        if (SelectedTable != null && previousHandId.HasValue)
                            SelectedHand = SelectedTable.Hands.FirstOrDefault(h => h.HandID == previousHandId.Value);
                    }

                    if (failed > 0)
                        SetStatusKey("Status_AutoRefreshed_ProcessedSkipped", processed, failed);
                    else
                        SetStatusKey("Status_AutoRefreshed_Processed", processed);
                    StatusKind = StatusKind.Done;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoRefresh] fatal: {ex}");
                _dispatcher.Invoke(() =>
                {
                    Status = string.Format(LocalizationManager.Instance["Status_Error_AutoRefreshPrefix"], ex.Message);
                    StatusKind = StatusKind.Error;
                });
            }
        }

        /// <summary>Non-blocking status push from the worker thread.</summary>
        private void PostStatus(string text)
        {
            // Use Normal priority (same as the terminal Dispatcher.Invoke) so FIFO
            // order is preserved. With Background priority, a late-arriving
            // "Scanning..." could fire AFTER "Done" and overwrite it.
            _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    // Ignore stale progress updates once scan has ended.
                    if (!IsScanning) return;
                    // Do not clear the keyed status; this is a transient progress update.
                    Status = text;
                }));
        }

        /// <summary>
        /// Like PostStatus but keeps the status as a localization key so it will
        /// update automatically when the language changes.
        /// </summary>
        private void PostStatusKey(string key, params object[] args)
        {
            _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (!IsScanning) return;
                    SetStatusKey(key, args);
                }));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_watcherSync)
            {
                DisposeWatcherCore();
            }
        }

        private void DisposeWatcherCore()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnWatchedFileChanged;
                _watcher.Created -= OnWatchedFileChanged;
                _watcher.Deleted -= OnWatchedFileChanged;
                _watcher.Renamed -= OnWatchedFileRenamed;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;
            }

            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = null;
        }
    }
}