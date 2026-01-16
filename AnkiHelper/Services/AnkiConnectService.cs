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
        private static readonly HttpClient _client = new();

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

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("http://localhost:8765", content);
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
            var tags = new List<string> { $"{vocabDTO.WordType}", $"{vocabDTO.Lesson}" };

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

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("http://127.0.0.1:8765", content);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task DeleteAdjTag(string deckName)
        {
            using var client = new HttpClient();

            // 1. get all note ids
            var noteIds = await CallAsync<List<long>>(new
            {
                action = "findNotes",
                version = 6,
                @params = new { query = $"deck:\"{deckName}\"" }
            });

            if (noteIds.Count == 0) return;

            // 2. get note info
            var notes = await CallAsync<List<JsonElement>>(new
            {
                action = "notesInfo",
                version = 6,
                @params = new { notes = noteIds }
            });

            foreach (var note in notes)
            {
                var tags = note.GetProperty("tags").EnumerateArray()
                    .Select(t => t.GetString())
                    .ToList();
                var id = note.GetProperty("noteId").GetInt64();

                var tagsToRemove = tags.Where(t => !int.TryParse(t, out _));
                if (!tagsToRemove.Any())
                    continue;

                try
                {
                    // remove old numeric tags
                    await CallAsync<object>(new
                    {
                        action = "removeTags",
                        version = 6,
                        @params = new
                        {
                            notes = new[] { id },
                            tags = string.Join(" ", tagsToRemove)
                        }
                    });
                }
                catch
                {

                }
            }
        }

        public async Task FixLessonTagsAsync(string deckName)
        {
            using var client = new HttpClient();

            // 1. get all note ids
            var noteIds = await CallAsync<List<long>>(new
            {
                action = "findNotes",
                version = 6,
                @params = new { query = $"deck:\"{deckName}\"" }
            });

            if (noteIds.Count == 0) return;

            // 2. get note info
            var notes = await CallAsync<List<JsonElement>>(new
            {
                action = "notesInfo",
                version = 6,
                @params = new { notes = noteIds }
            });

            foreach (var note in notes)
            {
                var id = note.GetProperty("noteId").GetInt64();
                var tags = note.GetProperty("tags").EnumerateArray()
                    .Select(t => t.GetString())
                    .ToList();

                var numeric = tags.Where(t => int.TryParse(t, out int d)).Select(t => int.Parse(t!)).OrderDescending().ToList();
                if (numeric.Count != 2) continue;

                if (numeric[0] < 5)
                    continue;

                var merged = $"{numeric[0]}-{numeric[1]}";
                if (tags.Contains(merged))
                {
                    await CallAsync<object>(new
                    {
                        action = "removeTags",
                        version = 6,
                        @params = new
                        {
                            notes = new[] { id },
                            tags = string.Join(" ", numeric)
                        }
                    });
                }

                try
                {
                    // add merged tag
                    await CallAsync<object>(new
                    {
                        action = "addTags",
                        version = 6,
                        @params = new
                        {
                            notes = new[] { id },
                            tags = merged
                        }
                    });

                    // remove old numeric tags
                    await CallAsync<object>(new
                    {
                        action = "removeTags",
                        version = 6,
                        @params = new
                        {
                            notes = new[] { id },
                            tags = string.Join(" ", numeric)
                        }
                    });
                }
                catch
                {

                }
            }
        }

        public async Task ApplyModernCssToDeckAsync(string deckName)
        {
            // 1. Get all note IDs in the deck
            var noteIds = await CallAsync<List<long>>(new
            {
                action = "findNotes",
                version = 6,
                @params = new { query = $"deck:\"{deckName}\"" }
            });

            if (noteIds.Count == 0)
            {
                Console.WriteLine("No notes found in deck.");
                return;
            }

            // 2. Get note info to find which models are used
            var notes = await CallAsync<List<JsonElement>>(new
            {
                action = "notesInfo",
                version = 6,
                @params = new { notes = noteIds }
            });

            var modelsInDeck = new HashSet<string>();
            foreach (var note in notes)
            {
                var modelName = note.GetProperty("modelName").GetString();
                if (modelName != null)
                    modelsInDeck.Add(modelName);
            }

            // 3. Modern dark-gray CSS
            string css = @"
.front, .back {
    font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif;
    font-size: 28px !important;
    font-weight: 700 !important;
    color: #e5e5e5 !important;
    background-color: #1e1e1e !important;
    text-align: center;
    line-height: 1.6;
    padding: 24px;
    border-radius: 12px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}
";

            // 4. Wrap fields in divs in all card templates for the models
            foreach (var modelName in modelsInDeck)
            {
                // Get the full model info
                var model = await CallAsync<JsonElement>(new
                {
                    action = "modelNamesAndIds",
                    version = 6
                });

                // Apply CSS
                await CallAsync<object>(new
                {
                    action = "updateModel",
                    version = 6,
                    @params = new
                    {
                        modelName,
                        css
                    }
                });

                // Optionally, you can also modify the card templates to wrap fields:
                // Front template: <div class='front'>{{Front}}</div>
                // Back template:  <div class='back'>{{Back}}</div>
                // This ensures the CSS applies correctly.
            }

            Console.WriteLine($"Modern dark CSS applied to all models in deck '{deckName}' successfully!");
        }

        private async Task<T> CallAsync<T>(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var res = await _client.PostAsync(
                "http://127.0.0.1:8765",
                new StringContent(json, Encoding.UTF8, "application/json")
            );
            var txt = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(txt)
                .GetProperty("result")
                .Deserialize<T>() ?? default!;
        }
    }
}
