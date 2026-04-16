using System.Windows;
using PokerLogsViewer.Services;

namespace PokerLogsViewer.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetCultureEn(object sender, RoutedEventArgs e)
        {
            LocalizationManager.Instance.SetCulture("en");
        }

        private void SetCultureRu(object sender, RoutedEventArgs e)
        {
            LocalizationManager.Instance.SetCulture("ru");
        }
    }
}
