namespace XianYuLauncher.ViewModels;

public sealed class ActionProposalChangeItem
{
    public string DisplayName { get; init; } = string.Empty;

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public string? Hint { get; init; }

    public string OldValueDisplay => string.IsNullOrWhiteSpace(OldValue) ? "未设置" : OldValue;

    public string NewValueDisplay => string.IsNullOrWhiteSpace(NewValue) ? "未设置" : NewValue;

    public bool HasHint => !string.IsNullOrWhiteSpace(Hint);
}