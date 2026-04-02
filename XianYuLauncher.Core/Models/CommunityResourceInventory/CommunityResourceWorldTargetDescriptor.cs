namespace XianYuLauncher.Core.Models;

public sealed class CommunityResourceWorldTargetDescriptor
{
    public string ResourceInstanceId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string WorldName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string TargetSaveName { get; init; } = string.Empty;
}