using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PokerLogsViewer.Services
{
    public sealed class FileScanner : IFileScanner
    {
        public IEnumerable<string> FindJsonFiles(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                yield break;

            foreach (var f in EnumerateSafe(rootPath))
                yield return f;
        }

        private static IEnumerable<string> EnumerateSafe(string path)
        {
            string[] files = null;
            string[] dirs = null;

            try { files = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly); }
            catch (System.Exception ex) { Debug.WriteLine($"[FileScanner] skip files in {path}: {ex.Message}"); }

            if (files != null)
                foreach (var f in files) yield return f;

            try { dirs = Directory.GetDirectories(path); }
            catch (System.Exception ex) { Debug.WriteLine($"[FileScanner] skip subdirs of {path}: {ex.Message}"); }

            if (dirs == null) yield break;

            foreach (var d in dirs)
                foreach (var f in EnumerateSafe(d))
                    yield return f;
        }
    }
}
