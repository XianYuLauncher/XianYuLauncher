using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.VersionAnalysis.Models;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// Freezes the batch-3 serialization boundary for version manifests.
/// Manifest files continue to use Newtonsoft.Json; config and external DTOs stay outside this helper.
/// </summary>
public static class VersionManifestJsonHelper
{
    private static readonly JsonSerializerSettings ManifestSerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    public static string SerializeVersionJson(VersionInfo versionInfo)
    {
        var serializer = JsonSerializer.Create(ManifestSerializerSettings);
        var jsonToken = JToken.FromObject(versionInfo, serializer);

        if (jsonToken is JObject jsonObject)
        {
            jsonObject.Remove("inheritsFrom");
        }

        return jsonToken.ToString(Formatting.Indented);
    }

    public static VersionInfo? DeserializeVersionInfo(string json)
    {
        return JsonConvert.DeserializeObject<VersionInfo>(json);
    }

    public static VersionManifest? DeserializeVersionManifest(string json)
    {
        return JsonConvert.DeserializeObject<VersionManifest>(json);
    }

    public static MinecraftVersionManifest? DeserializeMinecraftVersionManifest(string json)
    {
        return JsonConvert.DeserializeObject<MinecraftVersionManifest>(json);
    }

    public static JarVersionInfo? DeserializeJarVersionInfo(string json)
    {
        return JsonConvert.DeserializeObject<JarVersionInfo>(json);
    }
}