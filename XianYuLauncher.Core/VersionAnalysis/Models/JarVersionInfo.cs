using Newtonsoft.Json;

namespace XianYuLauncher.Core.VersionAnalysis.Models
{
    /// <summary>
    /// 对应 version.jar 中的 version.json 结构
    /// </summary>
    public class JarVersionInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("release_target")]
        public string ReleaseTarget { get; set; } = null!;

        [JsonProperty("world_version")]
        public int WorldVersion { get; set; }

        [JsonProperty("protocol_version")]
        public int ProtocolVersion { get; set; }
        
        [JsonProperty("pack_version")]
        public PackVersion PackVersion { get; set; } = null!;

        [JsonProperty("build_time")]
        public string BuildTime { get; set; } = null!;
        
        [JsonProperty("java_version")]
        public int JavaVersion { get; set; }
    }

    public class PackVersion
    {
         [JsonProperty("resource")]
         public int Resource { get; set; }
         
         [JsonProperty("data")]
         public int Data { get; set; }
    }
}
