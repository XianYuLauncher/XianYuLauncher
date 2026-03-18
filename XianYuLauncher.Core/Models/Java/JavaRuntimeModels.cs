using System.Collections.Generic;
using Newtonsoft.Json;

namespace XianYuLauncher.Core.Models;

public class JavaRuntimeManifest
{
    [JsonProperty("linux")]
    public Dictionary<string, List<JavaRuntimeVariant>> Linux { get; set; } = new();

    [JsonProperty("linux-i386")]
    public Dictionary<string, List<JavaRuntimeVariant>> LinuxI386 { get; set; } = new();

    [JsonProperty("mac-os")]
    public Dictionary<string, List<JavaRuntimeVariant>> MacOs { get; set; } = new();
    
    [JsonProperty("mac-os-arm64")]
    public Dictionary<string, List<JavaRuntimeVariant>> MacOsArm64 { get; set; } = new();

    [JsonProperty("windows-x64")]
    public Dictionary<string, List<JavaRuntimeVariant>> WindowsX64 { get; set; } = new();

    [JsonProperty("windows-x86")]
    public Dictionary<string, List<JavaRuntimeVariant>> WindowsX86 { get; set; } = new();
    
    [JsonProperty("windows-arm64")]
    public Dictionary<string, List<JavaRuntimeVariant>> WindowsArm64 { get; set; } = new();
    
    [JsonProperty("gamecore")]
    public Dictionary<string, List<JavaRuntimeVariant>> Gamecore { get; set; } = new();
}

public class JavaRuntimeVariant
{
    [JsonProperty("availability")]
    public JavaRuntimeAvailability Availability { get; set; } = null!;

    [JsonProperty("manifest")]
    public JavaRuntimeManifestRef Manifest { get; set; } = null!;

    [JsonProperty("version")]
    public JavaRuntimeVersion Version { get; set; } = null!;
}

public class JavaRuntimeAvailability
{
    [JsonProperty("group")]
    public int Group { get; set; }

    [JsonProperty("progress")]
    public int Progress { get; set; }
}

public class JavaRuntimeManifestRef
{
    [JsonProperty("sha1")]
    public string Sha1 { get; set; } = null!;

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; } = null!;
}

public class JavaRuntimeVersion
{
    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("released")]
    public string Released { get; set; } = null!;
}

public class JavaRuntimeFileManifest
{
    [JsonProperty("files")]
    public Dictionary<string, JavaRuntimeFile> Files { get; set; } = new();
}

public class JavaRuntimeFile
{
    [JsonProperty("type")]
    public string Type { get; set; } = null!; // "file" or "directory"

    [JsonProperty("downloads")]
    public JavaRuntimeDownloads Downloads { get; set; } = null!;

    [JsonProperty("executable")]
    public bool Executable { get; set; }
}

public class JavaRuntimeDownloads
{
    [JsonProperty("raw")]
    public JavaRuntimeDownloadInfo Raw { get; set; } = null!;

    [JsonProperty("lzma")]
    public JavaRuntimeDownloadInfo Lzma { get; set; } = null!;
}

public class JavaRuntimeDownloadInfo
{
    [JsonProperty("sha1")]
    public string Sha1 { get; set; } = null!;

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; } = null!;
}
