namespace XianYuLauncher.Features.Protocol;

public sealed class LaunchProtocolCommand : ProtocolCommand
{
    public LaunchProtocolCommand(
        Uri uri,
        string? targetPath,
        string? mapName,
        string? serverIp,
        string? serverPort)
        : base(uri)
    {
        TargetPath = targetPath;
        MapName = mapName;
        ServerIp = serverIp;
        ServerPort = serverPort;
    }

    public string? TargetPath { get; }

    public string? MapName { get; }

    public string? ServerIp { get; }

    public string? ServerPort { get; }
}
