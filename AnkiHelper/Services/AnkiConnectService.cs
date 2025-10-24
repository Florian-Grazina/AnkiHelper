using AnkiHelper.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkiHelper.Services
{
    internal partial class AnkiConnectService
    {
        public AnkiConnectService()
        {
        }

        public void EnsureAnkiIsRunning()
        {
            var processes = Process.GetProcessesByName("anki");
            if (processes.Length > 0)
                return;

            string[] possiblePaths =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Anki", "anki.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Anki", "anki.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Anki", "anki.exe")
            ];

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    var process = Process.Start(path);
                    process?.WaitForInputIdle(10000);

                    return;
                }
            }

            throw new FileNotFoundException("Could not find Anki installation.");
        }

        public async Task<IEnumerable<string>> GetDecksAsync()
        {
            var json = @"{
                ""action"": ""deckNames"",
                ""version"": 6
            }";

            using var client = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://localhost:8765", content);
            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            var decks = doc.RootElement.GetProperty("result");

            HashSet<string> deckNames = [];

            foreach (var item in decks.EnumerateArray())
            {
                string itemString = item.ToString();

                var match = DeckNameRegex().Match(itemString);
                if (match.Success)
                    itemString = match.Groups[1].Value;

                deckNames.Add(itemString);
            }

            return deckNames;
        }

        [GeneratedRegex(@"^(.*?)::L-\d+")]
        protected static partial Regex DeckNameRegex();

        public async Task<string> AddCardAsync(string deckName, VocabDTO vocabDTO)
        {
            var tags = new List<string> { $"{vocabDTO.WordType}", $"{vocabDTO.Lesson.Replace('-', ' ')}" };

            var request = new
            {
                action = "addNote",
                version = 6,
                @params = new
                {
                    note = new
                    {
                        deckName,
                        modelName = "Basic (and reversed card)",
                        fields = new
                        {
                            Front = vocabDTO.English,
                            Back = vocabDTO.Japanese
                        },
                        options = new
                        {
                            allowDuplicate = false
                        },
                        tags = tags
                    }
                }
            };

            using var client = new HttpClient();
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:8765", content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
