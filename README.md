# Poker Logs Viewer

A WPF desktop application (.NET 8) that recursively scans a folder for poker log
JSON files, parses them on a dedicated background thread, and displays results
grouped by table name in a 3-panel hierarchical UI.

## Requirements

- Windows 10 or 11 (x64)
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **Visual Studio 2022 (17.8 or later)** with the *.NET desktop development*
  workload — required for .NET 8 support (VS 2019 cannot open .NET 8 projects)
- **CMake 3.20+** — optional; you can build with the `dotnet` CLI directly

No NuGet packages are required. `System.Text.Json` ships in the runtime.

## Build

### Option A — `dotnet` CLI (simplest)

```powershell
dotnet build PokerLogsViewer.sln -c Release
dotnet run  --project src/PokerLogsViewer.csproj -c Release
```

### Option B — via CMake (wraps `dotnet`)

```powershell
cmake -B build -G "Visual Studio 17 2022"
cmake --build build --config Release
```

Output: `build/bin/Release/PokerLogsViewer.exe`

### Option C — self-contained single-file publish

```powershell
cmake -B build
cmake --build build --target publish
# → build/publish/PokerLogsViewer.exe  (no .NET runtime needed on target machine)
```

### Open in Visual Studio

Double-click `PokerLogsViewer.sln` — VS 2022 opens it without any manual fixes.

## Usage

1. Click **Browse...** and pick a folder containing `.json` poker logs.
2. Click **Start Scanning**. The button disables and the status bar shows
   `Scanning...` with live progress (`Scanning... N/Total`).
3. When done, the left panel lists unique table names (alphabetical), the
   middle panel lists hand IDs for the selected table, and the right panel
   shows full details of the selected hand.
4. Final status: `Done (N files processed)` or `Done (N processed, M skipped)`
   if some files were corrupted, or `Error: <message>` on fatal failure.

Invalid/corrupted files are skipped silently — the scan never crashes.

### Expected JSON shape

```json
[
  {
    "HandID": 123456789,
    "TableName": "Berlin #01",
    "Players": ["Jack", "Henry", "Daniel"],
    "Winners": ["Jack"],
    "WinAmount": "1 000,00$"
  }
]
```

A single object (not wrapped in an array) is also accepted. Property matching
is case-insensitive.

## Project structure

```
PokerLogsViewer/
├── CMakeLists.txt                 # wraps dotnet build
├── PokerLogsViewer.sln            # hand-written VS 2022 solution
├── README.md
├── .gitignore
└── src/
    ├── PokerLogsViewer.csproj     # SDK-style, net8.0-windows
    ├── App.xaml / App.xaml.cs     # composition root (manual DI)
    ├── Models/
    │   └── PokerHand.cs
    ├── Services/
    │   ├── IFileScanner.cs  / FileScanner.cs
    │   └── IJsonParser.cs   / JsonParser.cs   # System.Text.Json
    ├── ViewModels/
    │   ├── ViewModelBase.cs
    │   ├── RelayCommand.cs
    │   ├── TableGroupViewModel.cs
    │   └── MainViewModel.cs       # holds the Thread + Dispatcher logic
    └── Views/
        └── MainWindow.xaml / .cs  # code-behind: InitializeComponent only
```

## Architecture

```
Views  ──bindings──▶  ViewModels  ──interfaces──▶  Services
                          │
                          └──────▶  Models (POCO)
```

- **Views** — pure XAML. Code-behind is limited to `InitializeComponent()`.
- **ViewModels** — `MainViewModel` owns all state (`FolderPath`, `Status`,
  `Tables`, `SelectedTable`, `SelectedHand`) and commands (`BrowseCommand`,
  `ScanCommand`). Depends only on `IFileScanner` + `IJsonParser`.
- **Services** — `FileScanner` (recursive enumeration with per-directory error
  isolation), `JsonParser` (tolerant `System.Text.Json` deserialization; returns
  `null` on failure, never throws).
- **Models** — `PokerHand` POCO.
- **Composition root** — `App.OnStartup` wires everything together.

## Threading approach

All scanning runs on a single `System.Threading.Thread` (`IsBackground = true`).
**No `Task`, `async/await`, `ThreadPool`, or `BackgroundWorker` is used.**

The worker in `MainViewModel.ScanWorker`:

1. Enumerates `.json` files recursively via per-directory `Directory.GetFiles`
   calls wrapped in try/catch — a single inaccessible subdirectory does not
   abort the whole scan.
2. Parses each file with `System.Text.Json`, catching all exceptions per file.
   Corrupted files are counted as skipped and logged via `Debug.WriteLine`.
3. Builds a local `Dictionary<string, List<PokerHand>>` — no UI types are
   touched on this thread.
4. Sends throttled progress pings via `Dispatcher.BeginInvoke`
   (`DispatcherPriority.Background`, fire-and-forget).
5. On completion, performs **one** `Dispatcher.Invoke` that clears the
   `ObservableCollection` and populates it with sorted data.

An outer try/catch marshals any uncaught exception to the UI as
`Error: <message>` and releases `IsScanning`.

## UI synchronization strategy

- The background thread **never** mutates an `ObservableCollection` or a
  bindable property. Every UI mutation goes through
  `Application.Current.Dispatcher`, captured in the ViewModel constructor.
- `Dispatcher.Invoke` — terminal updates (must complete before the worker
  exits): clearing `Tables`, populating data, setting `IsScanning = false`.
- `Dispatcher.BeginInvoke` — progress pings (fire-and-forget, cheap, may be
  coalesced).
- `IsScanning` is `volatile` — read by UI thread in `CanExecute`, written on
  UI thread inside the terminal `Dispatcher.Invoke`.
- `ObservableCollection<T>` is mutated only on the UI thread, so no locks are
  needed. `TableGroupViewModel.Hands` is populated inside the same
  `Dispatcher.Invoke` that adds the parent to `Tables`, so the UI never sees a
  half-built group.

### Minimal thread + dispatcher example

```csharp
private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

void StartScan(string path)
{
    var t = new Thread(() =>
    {
        try
        {
            var data = HeavyWork(path);                   // background
            _dispatcher.Invoke(() => PopulateUi(data));   // UI thread
        }
        catch (Exception ex)
        {
            _dispatcher.Invoke(() => Status = "Error: " + ex.Message);
        }
    })
    { IsBackground = true, Name = "ScanThread" };
    t.Start();
}
```

## Possible extensions

- `FileSystemWatcher` on a separate `Thread` for auto-refresh on disk changes.
- Search/filter box over `Tables` / `Hands` via `CollectionViewSource`.
- Localization (RU/EN) through swappable `ResourceDictionary`.
- xUnit/MSTest test project covering `JsonParser` and `FileScanner`.
