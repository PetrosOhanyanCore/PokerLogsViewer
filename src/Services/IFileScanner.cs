using System.Collections.Generic;

namespace PokerLogsViewer.Services
{
    public interface IFileScanner
    {
        /// <summary>
        /// Lazily enumerates all *.json files under <paramref name="rootPath"/> recursively.
        /// Directories that throw (access denied, etc.) are skipped silently.
        /// </summary>
        IEnumerable<string> FindJsonFiles(string rootPath);
    }
}
