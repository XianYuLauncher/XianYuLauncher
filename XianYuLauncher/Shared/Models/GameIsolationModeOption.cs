namespace XianYuLauncher.Shared.Models;

public class GameIsolationModeOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public override string ToString()
    {
        return DisplayName;
    }
}