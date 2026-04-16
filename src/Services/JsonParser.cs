using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PokerLogsViewer.Models;

namespace PokerLogsViewer.Services
{
    public sealed class JsonParser : IJsonParser
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling         = JsonCommentHandling.Skip,
            AllowTrailingCommas         = true
        };

        public List<PokerHand> ParseFile(string filePath)
        {
            try
            {
                var text = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(text))
                    return null;

                var trimmed = text.TrimStart();
                if (trimmed.Length == 0) return null;

                // Files may be either a single object or an array of objects.
                if (trimmed[0] == '[')
                    return JsonSerializer.Deserialize<List<PokerHand>>(text, _options);

                var single = JsonSerializer.Deserialize<PokerHand>(text, _options);
                return single != null ? new List<PokerHand> { single } : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonParser] failed to parse '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}
