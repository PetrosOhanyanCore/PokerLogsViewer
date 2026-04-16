using System.Collections.Generic;

namespace PokerLogsViewer.Models
{
    /// <summary>
    /// POCO for a single poker hand record. Populated by System.Text.Json
    /// with case-insensitive property matching.
    /// </summary>
    public class PokerHand
    {
        public long HandID { get; set; }
        public string TableName { get; set; }
        public List<string> Players { get; set; } = new List<string>();
        public List<string> Winners { get; set; } = new List<string>();
        public string WinAmount { get; set; }
    }
}
