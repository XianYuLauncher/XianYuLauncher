namespace XianYuLauncher.Features.Protocol;

public abstract class ProtocolCommand
{
    protected ProtocolCommand(Uri uri)
    {
        Uri = uri;
    }

    public Uri Uri { get; }
}
