using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace PokerLogsViewer.Services
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        public static LocalizationManager Instance { get; } = new LocalizationManager();

        private readonly Dictionary<string, Dictionary<string, string>> _data;
        private string _culture = "en";

        private LocalizationManager()
        {
            _data = new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string>
                {
                    ["Title"] = "Poker Logs Viewer",
                    ["ButtonBrowse"] = "Browse...",
                    ["ButtonScan"] = "Start scan",
                    ["TablesHeader"] = "Tables",
                    ["Status_SelectFolder"] = "Select a folder and click 'Start scan'.",
                    ["Status_Error_FolderNotExist"] = "Error: Folder does not exist.",
                    ["Status_Scanning"] = "Scanning...",
                    ["Status_ScanningProgress"] = "Scanning... {0}/{1}",
                    ["Status_FoundFilesParsing"] = "Found {0} file(s). Parsing...",
                    ["Status_Done_Processed"] = "Done ({0} files processed)",
                    ["Status_Done_ProcessedSkipped"] = "Done ({0} files processed, {1} skipped)",
                    ["Status_Error_Prefix"] = "Error: {0}",
                    ["Status_AutoRefreshed_Processed"] = "Auto-refreshed ({0} files)",
                    ["Status_AutoRefreshed_ProcessedSkipped"] = "Auto-refreshed ({0} files, {1} skipped)",
                    ["Status_Error_AutoRefreshPrefix"] = "Auto-refresh error: {0}",
                    ["PlayersLabel"] = "Players",
                    ["WinnersLabel"] = "Winners",
                    ["WinAmountLabel"] = "Win Amount: ",
                    ["TableFilterToolTip"] = "Filter by table name",
                    ["NoDataPlaceholder"] = "No data. Click 'Start scan'.",
                    ["HandIdsHeader"] = "Hand IDs",
                    ["HandFilterToolTip"] = "Filter by Hand ID",
                    ["SelectTablePlaceholder"] = "Select a table",
                    ["DetailsHeader"] = "Details",
                    ["SelectHandPlaceholder"] = "Select a hand to view details"
                },
                ["ru"] = new Dictionary<string, string>
                {
                    ["Title"] = "\u041F\u0440\u043E\u0441\u043C\u043E\u0442\u0440 \u043B\u043E\u0433\u043E\u0432 \u043F\u043E\u043A\u0435\u0440\u0430",
                    ["ButtonBrowse"] = "\u041E\u0431\u0437\u043E\u0440...",
                    ["ButtonScan"] = "\u041D\u0430\u0447\u0430\u0442\u044C \u0441\u043A\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435.",
                    ["TablesHeader"] = "\u0421\u0442\u043E\u043B\u044B",
                    ["Status_SelectFolder"] = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u043F\u0430\u043F\u043A\u0443 \u0438 \u043D\u0430\u0436\u043C\u0438\u0442\u0435 '\u041D\u0430\u0447\u0430\u0442\u044C \u0441\u043A\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435'.",
                    ["Status_Scanning"] = "\u0421\u043A\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435...",
                    ["Status_ScanningProgress"] = "\u0421\u043A\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435... {0}/{1}",
                    ["Status_FoundFilesParsing"] = "\u041D\u0430\u0439\u0434\u0435\u043D\u043E {0} \u0444\u0430\u0439\u043B(\u043E\u0432). \u041F\u0430\u0440\u0441\u0438\u043D\u0433...",
                    ["Status_Done_Processed"] = "\u0413\u043E\u0442\u043E\u0432\u043E ({0} \u0444\u0430\u0439\u043B\u043E\u0432 \u043E\u0431\u0440\u0430\u0431\u043E\u0442\u0430)",
                    ["Status_Done_ProcessedSkipped"] = "\u0413\u043E\u0442\u043E\u0432\u043E ({0} \u0444\u0430\u0439\u043B\u043E\u0432 \u043E\u0431\u0440\u0430\u0431\u043E\u0442\u0430, {1} \u043F\u0440\u043E\u043F\u0443\u0449\u0435\u043D\u043E)",
                    ["Status_Error_Prefix"] = "\u041E\u0448\u0438\u0431\u043A\u0430: {0}",
                    ["Status_AutoRefreshed_Processed"] = "\u0410\u0432\u0442\u043E\u043E\u0431\u043D\u043E\u0432\u043B\u0435\u043D\u043E ({0} \u0444\u0430\u0439\u043B\u043E\u0432)",
                    ["Status_AutoRefreshed_ProcessedSkipped"] = "\u0410\u0432\u0442\u043E\u043E\u0431\u043D\u043E\u0432\u043B\u0435\u043D\u043E ({0} \u0444\u0430\u0439\u043B\u043E\u0432, {1} \u043F\u0440\u043E\u043F\u0443\u0449\u0435\u043D\u043E)",
                    ["Status_Error_AutoRefreshPrefix"] = "\u041E\u0448\u0438\u0431\u043A\u0430 \u0430\u0432\u0442\u043E\u043E\u0431\u043D\u043E\u0432\u043B\u0435\u043D\u0438\u044F: {0}",
                        ["PlayersLabel"] = "\u0418\u0433\u0440\u043E\u043A\u0438",
                        ["WinnersLabel"] = "\u041F\u043E\u0431\u0435\u0434\u0438\u0442\u0435\u043B\u0438",
                        ["WinAmountLabel"] = "\u0412\u044B\u0438\u0433\u0440\u044B\u0448:\u00A0",
                    ["TableFilterToolTip"] = "\u0424\u0438\u043B\u044C\u0442\u0440 \u043F\u043E \u0438\u043C\u0435\u043D\u0438 \u0441\u0442\u043E\u043B\u0430",
                    ["NoDataPlaceholder"] = "\u041D\u0435\u0442 \u0434\u0430\u043D\u043D\u044B\u0445. \u041D\u0430\u0436\u043C\u0438\u0442\u0435 '\u041D\u0430\u0447\u0430\u0442\u044C \u0441\u043A\u0430\u043D\u0438\u0440\u043E\u0432\u0430\u043D\u0438\u0435'.",
                    ["HandIdsHeader"] = "ID \u0420\u0430\u0437\u0434\u0430\u0447",
                    ["HandFilterToolTip"] = "\u0424\u0438\u043B\u044C\u0442\u0440 \u043F\u043E ID \u0440\u0430\u0437\u0434\u0430\u0447\u0438",
                    ["SelectTablePlaceholder"] = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0441\u0442\u043E\u043B",
                    ["DetailsHeader"] = "\u0414\u0435\u0442\u0430\u043B\u0438",
                    ["SelectHandPlaceholder"] = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0440\u0430\u0437\u0434\u0430\u0447\u0443 \u0434\u043B\u044F \u043F\u0440\u043E\u0441\u043C\u043E\u0442\u0440\u0430"
                }
                ,
                ["meta"] = new Dictionary<string, string>()
                {
                    
                }
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string this[string key]
        {
            get
            {
                if (_data.TryGetValue(_culture, out var dict) && dict.TryGetValue(key, out var val))
                    return val;

                // fallback
                if (_data.TryGetValue("en", out var en) && en.TryGetValue(key, out var ev))
                    return ev;

                return key;
            }
        }

        public string CurrentCulture => _culture;

        public void SetCulture(string culture)
        {
            culture = culture?.ToLowerInvariant() ?? "en";
            if (culture == _culture) return;
            _culture = culture;
            var ci = new CultureInfo(_culture);
            Thread.CurrentThread.CurrentUICulture = ci;
            Thread.CurrentThread.CurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}