using Newtonsoft.Json;

namespace LoraDbEditor.Models
{
    public class LoraEntry
    {
        [JsonProperty("active_triggers")]
        public string ActiveTriggers { get; set; } = string.Empty;

        [JsonProperty("all_triggers")]
        public string AllTriggers { get; set; } = string.Empty;

        [JsonProperty("file_id")]
        public string? FileId { get; set; }

        [JsonIgnore]
        public string Path { get; set; } = string.Empty;

        [JsonIgnore]
        public string FullPath { get; set; } = string.Empty;

        [JsonIgnore]
        public bool FileExists { get; set; }

        [JsonIgnore]
        public bool FileIdValid { get; set; }

        [JsonIgnore]
        public string? CalculatedFileId { get; set; }
    }
}
