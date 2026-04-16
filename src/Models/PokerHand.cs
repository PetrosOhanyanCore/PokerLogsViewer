using System.Collections.Generic;

namespace PokerLogsViewer.Models
{
    public class PokerHand
    {
        public long HandID { get; set; }
        public string TableName { get; set; }
        public List<string> Players { get; set; } = new List<string>();
        public List<string> Winners { get; set; } = new List<string>();
        public string WinAmount { get; set; }
    }
}