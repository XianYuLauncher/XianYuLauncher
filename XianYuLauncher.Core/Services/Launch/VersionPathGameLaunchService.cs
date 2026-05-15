using System.IO;
using Microsoft.Extensions.Logging;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services;

public sealed class PreparedVersionPathLaunch
{
    public required string VersionPath { get; init; }

    public required string VersionName { get; init; }

    public required string MinecraftPath { get; init; }
}

public sealed class VersionPathLaunchOptions
{
    public string? OverrideJavaPath { get; init; }

    public string? AccountId { get; init; }

    public string? QuickPlaySingleplayer { get; init; }

    public string? QuickPlayServer { get; init; }

    public int? QuickPlayPort { get; init; }
}

public interface IVersionPathGameLaunchService
{
    PreparedVersionPathLaunch PrepareLaunch(string versionPath);

    Task<GameLaunchResult> LaunchAsync(
        PreparedVersionPathLaunch preparedLaunch,
        VersionPathLaunchOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class VersionPathGameLaunchService : IVersionPathGameLaunchService
{
    private static readonly SemaphoreSlim LaunchPathSwitchGate = new(1, 1);
    private readonly IFileService _fileService;
    private readonly IGameLaunchService _gameLaunchService;
    private readonly ITokenRefreshService _tokenRefreshService;
    private readonly IAccountManager _accountManager;
    private readonly ILogger<VersionPathGameLaunchService> _logger;

    public VersionPathGameLaunchService(
        IFileService fileService,
        IGameLaunchService gameLaunchService,
        ITokenRefreshService tokenRefreshService,
        IAccountManager accountManager,
        ILogger<VersionPathGameLaunchService> logger)
    {
        _fileService = fileService;
        _gameLaunchService = gameLaunchService;
        _tokenRefreshService = tokenRefreshService;
        _accountManager = accountManager;
        _logger = logger;
    }

    public PreparedVersionPathLaunch PrepareLaunch(string versionPath)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(versionPath?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            throw new InvalidOperationException("缺少 path 参数，请使用 xianyulauncher://launch/?path=实例路径 格式。");
        }

        if (ProtocolPathSecurityHelper.IsUncPath(normalizedPath))
        {
            throw new InvalidOperationException("为了您的系统安全，已禁止从网络路径(UNC)加载游戏，请使用本地磁盘路径。");
        }

        if (!Directory.Exists(normalizedPath))
        {
            throw new InvalidOperationException("找不到目标实例路径。");
        }

        var versionDirectory = new DirectoryInfo(normalizedPath);
        var versionsDirectory = versionDirectory.Parent;
        var minecraftDirectory = versionsDirectory?.Parent;
        if (versionsDirectory == null
            || minecraftDirectory == null
            || !string.Equals(versionsDirectory.Name, MinecraftPathConsts.Versions, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("path 参数必须指向 versions 目录下的具体版本目录。");
        }

        if (string.IsNullOrWhiteSpace(versionDirectory.Name))
        {
            throw new InvalidOperationException("无法从目标路径解析版本名称。");
        }

        return new PreparedVersionPathLaunch
        {
            VersionPath = versionDirectory.FullName,
            VersionName = versionDirectory.Name,
            MinecraftPath = minecraftDirectory.FullName
        };
    }

    public async Task<GameLaunchResult> LaunchAsync(
        PreparedVersionPathLaunch preparedLaunch,
        VersionPathLaunchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparedLaunch);

        await LaunchPathSwitchGate.WaitAsync(cancellationToken);
        var launchOptions = options ?? new VersionPathLaunchOptions();
        var originalMinecraftPath = _fileService.GetMinecraftDataPath();
        var shouldSwitchMinecraftPath = !string.Equals(
            originalMinecraftPath,
            preparedLaunch.MinecraftPath,
            StringComparison.OrdinalIgnoreCase);

        try
        {
            if (shouldSwitchMinecraftPath)
            {
                _logger.LogInformation(
                    "按路径启动前临时切换游戏目录：{From} -> {To}",
                    originalMinecraftPath,
                    preparedLaunch.MinecraftPath);
                _fileService.SetMinecraftDataPath(preparedLaunch.MinecraftPath);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var accounts = await _accountManager.LoadAccountsAsync();
            var account = ResolveLaunchAccount(accounts, launchOptions.AccountId);
            if (account == null)
            {
                return new GameLaunchResult
                {
                    Success = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(launchOptions.AccountId)
                        ? "未选择任何账户，请先打开启动器登录。"
                        : "未找到指定账户，请确认账户仍然存在。"
                };
            }

            if (!account.IsOffline)
            {
                var refreshResult = await _tokenRefreshService.ValidateAndRefreshTokenAsync(account);
                if (!refreshResult.Success)
                {
                    return new GameLaunchResult
                    {
                        Success = false,
                        ErrorMessage = "账户登录已过期，请重新登录。"
                    };
                }

                account = refreshResult.UpdatedProfile ?? account;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await _gameLaunchService.LaunchGameAsync(
                preparedLaunch.VersionName,
                account,
                progress => { },
                status => { },
                cancellationToken,
                launchOptions.OverrideJavaPath,
                launchOptions.QuickPlaySingleplayer,
                launchOptions.QuickPlayServer,
                launchOptions.QuickPlayPort);
        }
        finally
        {
            if (shouldSwitchMinecraftPath)
            {
                try
                {
                    _fileService.SetMinecraftDataPath(originalMinecraftPath);
                    _logger.LogInformation(
                        "按路径启动后恢复游戏目录：{Path}",
                        originalMinecraftPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "恢复原始游戏目录失败：{Path}", originalMinecraftPath);
                }
            }

            LaunchPathSwitchGate.Release();
        }
    }

    private MinecraftAccount? ResolveLaunchAccount(List<MinecraftAccount> accounts, string? accountId)
    {
        if (accounts.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            return accounts.FirstOrDefault(account => string.Equals(account.Id, accountId, StringComparison.OrdinalIgnoreCase));
        }

        return _accountManager.GetActiveAccount(accounts);
    }
}