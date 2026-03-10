namespace XianYuLauncher.Features.Protocol;

public sealed class NavigateProtocolCommand : ProtocolCommand
{
    public NavigateProtocolCommand(Uri uri, string? page)
        : base(uri)
    {
        Page = page;
    }

    public string? Page { get; }
}
