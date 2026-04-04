namespace XianYuLauncher.Shared.Models;

public class JavaVersionInfo
{
    public string Version { get; set; } = string.Empty;

    public int MajorVersion { get; set; }

    public string Path { get; set; } = string.Empty;

    public bool IsJDK { get; set; }

    public override string ToString()
    {
        return $"Java {Version} {(IsJDK ? "(JDK)" : "(JRE)")} - {Path}";
    }
}