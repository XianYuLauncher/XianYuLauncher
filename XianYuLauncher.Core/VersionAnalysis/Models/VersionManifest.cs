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
        public string Id { get; set; }

        [JsonProperty("inheritsFrom")]
        public string InheritsFrom { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("mainClass")]
        public string MainClass { get; set; }

        [JsonProperty("libraries")]
        public List<Library> Libraries { get; set; }

        [JsonProperty("arguments")]
        public Arguments Arguments { get; set; }

        [JsonProperty("minecraftArguments")]
        public string MinecraftArguments { get; set; }
    }

    public class Library
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        // 其他字段按需添加，目前只用 Name 进行特征识别
    }

    public class Arguments
    {
        [JsonProperty("game")]
        public List<object> Game { get; set; }
        
        [JsonProperty("jvm")]
        public List<object> Jvm { get; set; }
    }
}
