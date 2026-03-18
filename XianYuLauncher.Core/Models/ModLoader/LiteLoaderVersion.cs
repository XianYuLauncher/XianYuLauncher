using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XianYuLauncher.Core.Models;

public class LiteLoaderRoot
{
    [JsonPropertyName("meta")]
    public LiteLoaderMeta Meta { get; set; } = null!;

    [JsonPropertyName("versions")]
    public Dictionary<string, LiteLoaderMcVersion> Versions { get; set; } = new();
}

public class LiteLoaderMeta
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("authors")]
    public string Authors { get; set; } = null!;

    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}

public class LiteLoaderMcVersion
{
    [JsonPropertyName("repo")]
    public LiteLoaderRepoMetadata Repo { get; set; } = null!;

    [JsonPropertyName("snapshots")]
    public LiteLoaderSnapshots Snapshots { get; set; } = null!;

    [JsonPropertyName("artefacts")]
    public LiteLoaderSnapshots Artefacts { get; set; } = null!;
}

public class LiteLoaderRepoMetadata
{
    [JsonPropertyName("stream")]
    public string Stream { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("classifier")]
    public string Classifier { get; set; } = null!;
}

public class LiteLoaderSnapshots
{
    [JsonPropertyName("libraries")]
    public List<LiteLoaderLibrary> Libraries { get; set; } = new();

    [JsonPropertyName("com.mumfrey:liteloader")]
    public Dictionary<string, LiteLoaderArtifact> Artifacts { get; set; } = new();
}

public class LiteLoaderArtifact
{
    [JsonPropertyName("stream")]
    public string Stream { get; set; } = null!;

    [JsonPropertyName("file")]
    public string File { get; set; } = null!;

    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = null!;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = null!;
    
    [JsonPropertyName("tweakClass")]
    public string TweakClass { get; set; } = null!;

    [JsonPropertyName("libraries")]
    public List<LiteLoaderLibrary> Libraries { get; set; } = new();

    [JsonPropertyName("build")]
    public string Build { get; set; } = null!;

    [JsonIgnore]
    public string? BaseUrl { get; set; }
}

public class LiteLoaderLibrary
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}
