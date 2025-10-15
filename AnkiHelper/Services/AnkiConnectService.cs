using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkiHelper.Services
{
    internal partial class AnkiConnectService
    {
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

            foreach(var item in decks.EnumerateArray())
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
    }
}
