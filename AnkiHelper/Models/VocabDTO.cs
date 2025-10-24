using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace AnkiHelper.Models
{
    internal struct VocabDTO
    {
        public string Lesson { get; }
        [JsonConverter(typeof(StringEnumConverter))]
        public WordTypeEnum WordType{ get; set; }
        public string Japanese { get; set; }
        public string English { get; set; }
    }
}
