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

        [JsonProperty("source_url")]
        public string? SourceUrl { get; set; }

        [JsonProperty("suggested_strength")]
        public string? SuggestedStrength { get; set; }

        [JsonProperty("notes")]
        public string? Notes { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("gallery")]
        public List<string>? Gallery { get; set; }

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
