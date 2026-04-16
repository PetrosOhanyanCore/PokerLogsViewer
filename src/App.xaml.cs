using System.Windows;
using PokerLogsViewer.Services;
using PokerLogsViewer.ViewModels;
using PokerLogsViewer.Views;

namespace PokerLogsViewer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Manual DI — single place that knows concrete types.
            IFileScanner scanner = new FileScanner();
            IJsonParser  parser  = new JsonParser();
            var vm               = new MainViewModel(scanner, parser);

            var window = new MainWindow { DataContext = vm };
            window.Closed += (_, __) => vm.Dispose();
            window.Show();
        }
    }
}
