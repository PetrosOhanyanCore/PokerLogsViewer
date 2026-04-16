using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PokerLogsViewer.Services
{
    public sealed class FileScanner : IFileScanner
    {
        private static readonly string[] AcceptedExtensions =
  {
            ".json",
            ".json.txt"
        };

        public IEnumerable<string> FindJsonFiles(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                yield break;

            foreach (var f in EnumerateSafe(rootPath))
                yield return f;
        }

        private static IEnumerable<string> EnumerateSafe(string path)
        {
            string[] allFiles = null;
            string[] dirs = null;

            try
            {
                allFiles = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileScanner] skip files in {path}: {ex.Message}");
            }

            if (allFiles != null)
            {
                foreach (var f in allFiles)
                {
                    var name = Path.GetFileName(f);
                    if (HasAcceptedExtension(name))
                        yield return f;
                }
            }

            try { dirs = Directory.GetDirectories(path); }
            catch (Exception ex) { Debug.WriteLine($"[FileScanner] skip subdirs of {path}: {ex.Message}"); }

            if (dirs == null) yield break;

            foreach (var d in dirs)
                foreach (var f in EnumerateSafe(d))
                    yield return f;
        }

        private static bool HasAcceptedExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            for (int i = 0; i < AcceptedExtensions.Length; i++)
            {
                if (fileName.EndsWith(AcceptedExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}