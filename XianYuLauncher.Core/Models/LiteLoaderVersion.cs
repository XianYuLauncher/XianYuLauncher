using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

public class LiteLoaderRoot
{
    [JsonPropertyName("meta")]
    public LiteLoaderMeta Meta { get; set; }

    [JsonPropertyName("versions")]
    public Dictionary<string, LiteLoaderMcVersion> Versions { get; set; }
}

public class LiteLoaderMeta
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("authors")]
    public string Authors { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class LiteLoaderMcVersion
{
    [JsonPropertyName("repo")]
    public LiteLoaderRepoMetadata Repo { get; set; }

    [JsonPropertyName("snapshots")]
    public LiteLoaderSnapshots Snapshots { get; set; }

    [JsonPropertyName("artefacts")]
    public LiteLoaderSnapshots Artefacts { get; set; }
}

public class LiteLoaderRepoMetadata
{
    [JsonPropertyName("stream")]
    public string Stream { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("classifier")]
    public string Classifier { get; set; }
}

public class LiteLoaderSnapshots
{
    [JsonPropertyName("libraries")]
    public List<LiteLoaderLibrary> Libraries { get; set; }

    [JsonPropertyName("com.mumfrey:liteloader")]
    public Dictionary<string, LiteLoaderArtifact> Artifacts { get; set; }
}

public class LiteLoaderArtifact
{
    [JsonPropertyName("stream")]
    public string Stream { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }
    
    [JsonPropertyName("tweakClass")]
    public string TweakClass { get; set; }

    [JsonPropertyName("libraries")]
    public List<LiteLoaderLibrary> Libraries { get; set; }

    [JsonPropertyName("build")]
    public string Build { get; set; }

    [JsonIgnore]
    public string? BaseUrl { get; set; }
}

public class LiteLoaderLibrary
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
