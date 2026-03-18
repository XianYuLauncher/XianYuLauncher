using System.Collections.Generic;
using Newtonsoft.Json;

namespace XianYuLauncher.Core.VersionAnalysis.Models
{
    /// <summary>
    /// 对应 .minecraft/versions/{id}/{id}.json 的结构
    /// </summary>
    public class MinecraftVersionManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("inheritsFrom")]
        public string InheritsFrom { get; set; } = null!;

        [JsonProperty("type")]
        public string Type { get; set; } = null!;

        [JsonProperty("mainClass")]
        public string MainClass { get; set; } = null!;

        [JsonProperty("libraries")]
        public List<Library> Libraries { get; set; } = new();

        [JsonProperty("arguments")]
        public Arguments Arguments { get; set; } = null!;

        [JsonProperty("minecraftArguments")]
        public string MinecraftArguments { get; set; } = null!;
    }

    public class Library
    {
        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("url")]
        public string Url { get; set; } = null!;

        // 其他字段按需添加，目前只用 Name 进行特征识别
    }

    public class Arguments
    {
        [JsonProperty("game")]
        public List<object> Game { get; set; } = new();
        
        [JsonProperty("jvm")]
        public List<object> Jvm { get; set; } = new();
    }
}
