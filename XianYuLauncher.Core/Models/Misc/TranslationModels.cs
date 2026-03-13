using Newtonsoft.Json;
using System;

namespace XianYuLauncher.Core.Models
{
    /// <summary>
    /// MCIM 翻译响应模型
    /// </summary>
    public class McimTranslationResponse
    {
        [JsonProperty("project_id")]
        public string ProjectId { get; set; }
        
        [JsonProperty("modid")]
        public int? ModId { get; set; }
        
        [JsonProperty("translated")]
        public string Translated { get; set; }
        
        [JsonProperty("original")]
        public string Original { get; set; }
        
        [JsonProperty("translated_at")]
        public DateTime TranslatedAt { get; set; }
    }
}
