using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PokerLogsViewer.Models;
using PokerLogsViewer.Services;

namespace PokerLogsViewer.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private readonly IFileScanner _fileScanner;
        private readonly IJsonParser _jsonParser;
        private readonly Dispatcher _dispatcher;

        private Thread _scanThread;
        // Volatile because it is read/written by both UI and worker threads.
        private volatile bool _isScanning;

        private string _folderPath;
        private string _status = "Select a folder and click Start Scanning.";
        private StatusKind _statusKind = StatusKind.Idle;
        private TableGroupViewModel _selectedTable;
        private PokerHand _selectedHand;

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

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Drives the status-bar text color via DataTriggers in XAML.
        /// </summary>
        public StatusKind StatusKind
        {
            get => _statusKind;
            private set => SetProperty(ref _statusKind, value);
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
                    SelectedHand = null; // reset detail panel
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
            if (IsScanning) return;

            var path = FolderPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Status = "✖ Error: folder does not exist.";
                StatusKind = StatusKind.Error;
                return;
            }

            // Reset UI state on UI thread before leaving.
            IsScanning = true;
            Status = "Scanning...";
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
            int processed = 0;
            int failed = 0;

            try
            {
                // 1. Enumerate files (pure background work).
                var files = _fileScanner.FindJsonFiles(rootPath).ToList();
                int total = files.Count;

                PostStatus($"Found {total} file(s). Parsing...");

                // 2. Parse into a local dictionary (no UI touch).
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
                        Debug.WriteLine($"[ScanWorker] file '{file}': {ex.Message}");
                        failed++;
                    }

                    // Throttled progress update — fire-and-forget, does not block worker.
                    if ((i & 0x1F) == 0)
                        PostStatus($"Scanning... {i + 1}/{total}");
                }

                // 3. Hand off to the UI thread in a single batched update.
                _dispatcher.Invoke(new Action(() =>
                {
                    Tables.Clear();
                    foreach (var kv in grouped.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var group = new TableGroupViewModel(kv.Key);
                        foreach (var h in kv.Value.OrderBy(h => h.HandID))
                            group.Hands.Add(h);
                        Tables.Add(group);
                    }

                    Status = failed > 0
                        ? $"✔ Done ({processed} files processed, {failed} skipped)"
                        : $"✔ Done ({processed} files processed)";
                    StatusKind = StatusKind.Done;

                    IsScanning = false;
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScanWorker] fatal: {ex}");
                _dispatcher.Invoke(new Action(() =>
                {
                    Status = $"✖ Error: {ex.Message}";
                    StatusKind = StatusKind.Error;
                    IsScanning = false;
                }));
            }
        }

        /// <summary>Non-blocking status push from the worker thread.</summary>
        private void PostStatus(string text)
        {
            // Use Normal priority (same as the terminal Dispatcher.Invoke) so FIFO
            // order is preserved. With Background priority, a late-arriving
            // "Scanning..." could fire AFTER "Done" and overwrite it.
            _dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => Status = text));
        }
    }
}