using fNbt;
using System.IO.Compression;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Features.VersionList.ViewModels;
using XianYuLauncher.Helpers;
using XianYuLauncher.Models.VersionManagement;
using Windows.Storage;

namespace XianYuLauncher.Features.VersionManagement.Services;

public sealed class OverviewDataService : IOverviewDataService
{
    private readonly IVersionConfigService _versionConfigService;

    public OverviewDataService(IVersionConfigService versionConfigService)
    {
        _versionConfigService = versionConfigService;
    }

    public async Task<(int LaunchCount, long TotalPlayTimeSeconds, DateTime? LastLaunchTime)?> LoadOverviewDataAsync(
        VersionListViewModel.VersionInfoItem? selectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (selectedVersion == null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var config = await _versionConfigService.LoadConfigAsync(selectedVersion.Name);
        return (config.LaunchCount, config.TotalPlayTimeSeconds, config.LastLaunchTime);
    }

    public async Task<List<SaveInfo>> LoadSavesAsync(
        VersionListViewModel.VersionInfoItem? selectedVersion,
        string? gameDir = null,
        CancellationToken cancellationToken = default)
    {
        if (selectedVersion == null || cancellationToken.IsCancellationRequested)
        {
            return new List<SaveInfo>();
        }

        var basePath = !string.IsNullOrEmpty(gameDir) ? gameDir : selectedVersion.Path;
        var savesPath = Path.Combine(basePath, MinecraftPathConsts.Saves);
        if (!Directory.Exists(savesPath))
        {
            return new List<SaveInfo>();
        }

        var saveDirectories = Directory.GetDirectories(savesPath);
        var saveInfos = new List<SaveInfo>();

        foreach (var saveDir in saveDirectories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var saveName = Path.GetFileName(saveDir);
            var levelDatPath = Path.Combine(saveDir, "level.dat");
            if (!File.Exists(levelDatPath))
            {
                continue;
            }

            var saveInfo = new SaveInfo
            {
                Name = saveName,
                Path = saveDir,
                DisplayName = saveName,
                LastPlayed = Directory.GetLastWriteTime(saveDir)
            };

            try
            {
                var levelData = await ReadLevelDatAsync(levelDatPath);
                if (levelData != null)
                {
                    if (!string.IsNullOrEmpty(levelData.LevelName))
                    {
                        saveInfo.DisplayName = levelData.LevelName;
                    }

                    saveInfo.GameMode = levelData.GameType switch
                    {
                        0 => "VersionManagerPage_GameMode_Survival".GetLocalized(),
                        1 => "VersionManagerPage_GameMode_Creative".GetLocalized(),
                        2 => "VersionManagerPage_GameMode_Adventure".GetLocalized(),
                        3 => "VersionManagerPage_GameMode_Spectator".GetLocalized(),
                        _ => "VersionManagerPage_GameMode_Unknown".GetLocalized()
                    };

                    if (levelData.LastPlayed > 0)
                    {
                        saveInfo.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(levelData.LastPlayed).LocalDateTime;
                    }
                }
            }
            catch
            {
            }

            saveInfos.Add(saveInfo);
        }

        return saveInfos.OrderByDescending(save => save.LastPlayed).ToList();
    }

    public async Task LoadSaveIconsAsync(
        IReadOnlyList<SaveInfo> saves,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            foreach (var save in saves)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var iconPath = Path.Combine(save.Path, "icon.png");
                    if (File.Exists(iconPath))
                    {
                        save.Icon = iconPath;
                    }
                }
                catch
                {
                }
            }
        }, cancellationToken);
    }

    public Task<List<ScreenshotInfo>> LoadScreenshotsAsync(
        string screenshotsPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || !Directory.Exists(screenshotsPath))
        {
            return Task.FromResult(new List<ScreenshotInfo>());
        }

        var screenshotFiles = Directory.GetFiles(screenshotsPath, "*.png");
        var screenshots = screenshotFiles
            .Select(filePath => new ScreenshotInfo(filePath))
            .OrderByDescending(screenshot => screenshot.OriginalCreationTime)
            .ToList();

        return Task.FromResult(screenshots);
    }

    public List<ScreenshotInfo> FilterScreenshots(
        IReadOnlyList<ScreenshotInfo> allScreenshots,
        string screenshotSearchText)
    {
        if (string.IsNullOrWhiteSpace(screenshotSearchText))
        {
            return allScreenshots.ToList();
        }

        return allScreenshots
            .Where(screenshot => screenshot.FileName.Contains(screenshotSearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public bool HasSameScreenshotSnapshot(
        IEnumerable<ScreenshotInfo> currentItems,
        IEnumerable<ScreenshotInfo> sourceItems)
    {
        static HashSet<string> BuildPathSet(IEnumerable<ScreenshotInfo> items)
        {
            return items
                .Select(item => item.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var currentSet = BuildPathSet(currentItems);
        var sourceSet = BuildPathSet(sourceItems);
        return currentSet.SetEquals(sourceSet);
    }

    public async Task DeleteScreenshotAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        await Task.Run(() => File.Delete(filePath));
    }

    public async Task CopyScreenshotAsync(string sourceFilePath, StorageFile destinationFile)
    {
        var sourceFile = await StorageFile.GetFileFromPathAsync(sourceFilePath);
        await sourceFile.CopyAndReplaceAsync(destinationFile);
    }

    public (string? RandomScreenshotPath, bool HasRandomScreenshot) PickRandomScreenshot(IReadOnlyList<ScreenshotInfo> screenshots)
    {
        if (screenshots.Count == 0)
        {
            return (null, false);
        }

        var random = new Random();
        var index = random.Next(screenshots.Count);
        return (screenshots[index].FilePath, true);
    }

    private static async Task<LevelDatInfo?> ReadLevelDatAsync(string levelDatPath)
    {
        try
        {
            await using var fileStream = File.OpenRead(levelDatPath);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            await gzipStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var nbtFile = new NbtFile();
            nbtFile.LoadFromStream(memoryStream, NbtCompression.None);

            var dataTag = nbtFile.RootTag["Data"] as NbtCompound;
            if (dataTag == null)
            {
                return null;
            }

            return new LevelDatInfo
            {
                LevelName = dataTag["LevelName"]?.StringValue ?? string.Empty,
                GameType = dataTag["GameType"]?.IntValue ?? 0,
                LastPlayed = dataTag["LastPlayed"]?.LongValue ?? 0
            };
        }
        catch
        {
            return null;
        }
    }
}