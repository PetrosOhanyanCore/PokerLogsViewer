using System.Collections.Generic;
using PokerLogsViewer.Models;

namespace PokerLogsViewer.Services
{
    public interface IJsonParser
    {
        /// <summary>
        /// Parses a file into a list of <see cref="PokerHand"/>.
        /// Returns <c>null</c> if the file is corrupted or unreadable (never throws).
        /// </summary>
        List<PokerHand> ParseFile(string filePath);
    }
}
