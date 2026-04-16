using System.Collections.ObjectModel;
using PokerLogsViewer.Models;

namespace PokerLogsViewer.ViewModels
{
    /// <summary>
    /// Groups all hands that share a TableName. Exposed to the UI as a single
    /// item in the left-panel list; its <see cref="Hands"/> feeds the middle panel.
    /// </summary>
    public sealed class TableGroupViewModel : ViewModelBase
    {
        public string TableName { get; }

        public ObservableCollection<PokerHand> Hands { get; } = new ObservableCollection<PokerHand>();

        public TableGroupViewModel(string tableName)
        {
            TableName = tableName;
        }

        public override string ToString() => TableName;
    }
}
