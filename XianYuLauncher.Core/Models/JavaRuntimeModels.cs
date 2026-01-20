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
    public JavaRuntimeAvailability Availability { get; set; }

    [JsonProperty("manifest")]
    public JavaRuntimeManifestRef Manifest { get; set; }

    [JsonProperty("version")]
    public JavaRuntimeVersion Version { get; set; }
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
    public string Sha1 { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}

public class JavaRuntimeVersion
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("released")]
    public string Released { get; set; }
}

public class JavaRuntimeFileManifest
{
    [JsonProperty("files")]
    public Dictionary<string, JavaRuntimeFile> Files { get; set; } = new();
}

public class JavaRuntimeFile
{
    [JsonProperty("type")]
    public string Type { get; set; } // "file" or "directory"

    [JsonProperty("downloads")]
    public JavaRuntimeDownloads Downloads { get; set; }

    [JsonProperty("executable")]
    public bool Executable { get; set; }
}

public class JavaRuntimeDownloads
{
    [JsonProperty("raw")]
    public JavaRuntimeDownloadInfo Raw { get; set; }

    [JsonProperty("lzma")]
    public JavaRuntimeDownloadInfo Lzma { get; set; }
}

public class JavaRuntimeDownloadInfo
{
    [JsonProperty("sha1")]
    public string Sha1 { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}
