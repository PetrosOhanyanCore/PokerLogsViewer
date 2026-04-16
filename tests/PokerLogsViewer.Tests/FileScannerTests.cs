using System.IO;
using System.Linq;
using PokerLogsViewer.Services;
using Xunit;

namespace PokerLogsViewer.Tests
{
    public class FileScannerTests
    {
        [Fact]
        public void FindJsonFiles_FindsJsonAndJsonTxt()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);

            var f1 = Path.Combine(dir, "a.json");
            var f2 = Path.Combine(dir, "b.json.txt");
            var f3 = Path.Combine(dir, "c.txt");

            File.WriteAllText(f1, "{}");
            File.WriteAllText(f2, "{}");
            File.WriteAllText(f3, "{}");

            var scanner = new FileScanner();
            var found = scanner.FindJsonFiles(dir).ToList();

            Assert.Contains(f1, found);
            Assert.Contains(f2, found);
            Assert.DoesNotContain(f3, found);
        }
    }
}
