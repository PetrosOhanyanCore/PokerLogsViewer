using System.Collections.Generic;
using System.IO;
using PokerLogsViewer.Models;
using PokerLogsViewer.Services;
using Xunit;

namespace PokerLogsViewer.Tests
{
    public class JsonParserTests
    {
        private readonly JsonParser _parser = new();

        [Fact]
        public void ParseFile_ReturnsNull_ForEmptyFile()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "   \r\n  \t   ");

            var result = _parser.ParseFile(path);

            Assert.Null(result);
        }

        [Fact]
        public void ParseFile_ReturnsSingleItem_ForSingleObject()
        {
            var json = "{ \"HandID\": 123, \"TableName\": \"TestTable\", \"Players\": [\"A\", \"B\"], \"Winners\": [\"A\"], \"WinAmount\": \"$10\" }";
            var path = Path.GetTempFileName();
            File.WriteAllText(path, json);

            var result = _parser.ParseFile(path);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(123, result[0].HandID);
            Assert.Equal("TestTable", result[0].TableName);
        }

        [Fact]
        public void ParseFile_ReturnsList_ForArray()
        {
            var json = "[ { \"HandID\": 1, \"TableName\": \"T1\" }, { \"HandID\": 2, \"TableName\": \"T2\" } ]";
            var path = Path.GetTempFileName();
            File.WriteAllText(path, json);

            var result = _parser.ParseFile(path);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].HandID);
            Assert.Equal(2, result[1].HandID);
        }

        [Fact]
        public void ParseFile_ReturnsNull_ForInvalidJson()
        {
            var json = "{ this is not valid json }";
            var path = Path.GetTempFileName();
            File.WriteAllText(path, json);

            var result = _parser.ParseFile(path);

            Assert.Null(result);
        }
    }
}
