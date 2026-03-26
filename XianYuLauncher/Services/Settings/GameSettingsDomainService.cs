using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Services.Settings;

public class GameSettingsDomainService : IGameSettingsDomainService
{
    private const string JavaPathKey = "JavaPath";
    private const string SelectedJavaVersionKey = "SelectedJavaVersion";
    private const string JavaVersionsKey = "JavaVersions";
    private const string EnableVersionIsolationKey = "EnableVersionIsolation";
    private const string GameIsolationModeKey = "GameIsolationMode";
    private const string CustomGameDirectoryKey = "CustomGameDirectory";
    private const string EnableRealTimeLogsKey = "EnableRealTimeLogs";
    private const string JavaSelectionModeKey = "JavaSelectionMode";
    private const string MinecraftPathKey = "MinecraftPath";
    private const string MinecraftPathsKey = "MinecraftPaths";

    private const string GlobalAutoMemoryKey = "GlobalAutoMemoryAllocation";
    private const string GlobalInitialHeapKey = "GlobalInitialHeapMemory";
    private const string GlobalMaxHeapKey = "GlobalMaximumHeapMemory";
    private const string GlobalCustomJvmArgumentsKey = "GlobalCustomJvmArguments";
    private const string GlobalGarbageCollectorModeKey = "GlobalGarbageCollectorMode";
    private const string GlobalWindowWidthKey = "GlobalWindowWidth";
    private const string GlobalWindowHeightKey = "GlobalWindowHeight";

    private readonly ISettingsRepository _settingsRepository;
    private readonly IFileService _fileService;

    public GameSettingsDomainService(ISettingsRepository settingsRepository, IFileService fileService)
    {
        _settingsRepository = settingsRepository;
        _fileService = fileService;
    }

    public async Task<bool> LoadEnableRealTimeLogsAsync()
    {
        return await _settingsRepository.ReadAsync<bool?>(EnableRealTimeLogsKey) ?? false;
    }

    public Task SaveEnableRealTimeLogsAsync(bool value)
    {
        return _settingsRepository.SaveAsync(EnableRealTimeLogsKey, value);
    }

    public async Task<bool> LoadEnableVersionIsolationAsync()
    {
        return await _settingsRepository.ReadAsync<bool?>(EnableVersionIsolationKey) ?? true;
    }

    public Task SaveEnableVersionIsolationAsync(bool value)
    {
        return _settingsRepository.SaveAsync(EnableVersionIsolationKey, value);
    }

    public Task<string?> LoadGameIsolationModeAsync()
    {
        return _settingsRepository.ReadAsync<string>(GameIsolationModeKey);
    }

    public Task SaveGameIsolationModeAsync(string value)
    {
        return _settingsRepository.SaveAsync(GameIsolationModeKey, value);
    }

    public Task<string?> LoadCustomGameDirectoryAsync()
    {
        return _settingsRepository.ReadAsync<string>(CustomGameDirectoryKey);
    }

    public Task SaveCustomGameDirectoryAsync(string value)
    {
        return _settingsRepository.SaveAsync(CustomGameDirectoryKey, value);
    }

    public Task<string?> LoadJavaSelectionModeAsync()
    {
        return LoadJavaSelectionModeCoreAsync();
    }

    public Task SaveJavaSelectionModeAsync(string value)
    {
        Log.Information("[GameSettings.JavaSelectionMode] Save requested. Value={Value}", value);
        return _settingsRepository.SaveAsync(JavaSelectionModeKey, value);
    }

    private async Task<string?> LoadJavaSelectionModeCoreAsync()
    {
        var value = await _settingsRepository.ReadAsync<string>(JavaSelectionModeKey);
        Log.Information("[GameSettings.JavaSelectionMode] Load completed. Value={Value}", value ?? "(null)");
        return value;
    }

    public Task<string?> LoadJavaPathAsync()
    {
        return _settingsRepository.ReadAsync<string>(JavaPathKey);
    }

    public Task SaveJavaPathAsync(string value)
    {
        return _settingsRepository.SaveAsync(JavaPathKey, value);
    }

    public async Task SaveSelectedJavaVersionAsync(string value)
    {
        await _settingsRepository.SaveAsync(SelectedJavaVersionKey, value);
        await _settingsRepository.SaveAsync(JavaPathKey, value);
    }

    public async Task ClearJavaSelectionAsync()
    {
        await _settingsRepository.SaveAsync(JavaPathKey, string.Empty);
        await _settingsRepository.SaveAsync(SelectedJavaVersionKey, string.Empty);
    }

    public async Task<IReadOnlyList<XianYuLauncher.Core.Models.JavaVersion>?> LoadJavaVersionsAsync()
    {
        return await _settingsRepository.ReadAsync<List<XianYuLauncher.Core.Models.JavaVersion>>(JavaVersionsKey);
    }

    public Task SaveJavaVersionsAsync(IReadOnlyList<XianYuLauncher.Core.Models.JavaVersion> versions)
    {
        return _settingsRepository.SaveAsync(JavaVersionsKey, versions);
    }

    public async Task<GameGlobalLaunchSettingsState> LoadGlobalLaunchSettingsAsync()
    {
        return new GameGlobalLaunchSettingsState
        {
            AutoMemoryAllocation = await _settingsRepository.ReadAsync<bool?>(GlobalAutoMemoryKey) ?? true,
            InitialHeapMemory = await _settingsRepository.ReadAsync<double?>(GlobalInitialHeapKey) ?? 6.0,
            MaximumHeapMemory = await _settingsRepository.ReadAsync<double?>(GlobalMaxHeapKey) ?? 12.0,
            CustomJvmArguments = await _settingsRepository.ReadAsync<string>(GlobalCustomJvmArgumentsKey) ?? string.Empty,
            GarbageCollectorMode = GarbageCollectorModeHelper.Normalize(await _settingsRepository.ReadAsync<string>(GlobalGarbageCollectorModeKey)),
            WindowWidth = await _settingsRepository.ReadAsync<int?>(GlobalWindowWidthKey) ?? 1280,
            WindowHeight = await _settingsRepository.ReadAsync<int?>(GlobalWindowHeightKey) ?? 720
        };
    }

    public Task SaveGlobalAutoMemoryAllocationAsync(bool value)
    {
        return _settingsRepository.SaveAsync(GlobalAutoMemoryKey, value);
    }

    public Task SaveGlobalInitialHeapMemoryAsync(double value)
    {
        return _settingsRepository.SaveAsync(GlobalInitialHeapKey, value);
    }

    public Task SaveGlobalMaximumHeapMemoryAsync(double value)
    {
        return _settingsRepository.SaveAsync(GlobalMaxHeapKey, value);
    }

    public Task SaveGlobalCustomJvmArgumentsAsync(string value)
    {
        return _settingsRepository.SaveAsync(GlobalCustomJvmArgumentsKey, value);
    }

    public Task SaveGlobalGarbageCollectorModeAsync(string value)
    {
        return _settingsRepository.SaveAsync(GlobalGarbageCollectorModeKey, GarbageCollectorModeHelper.Normalize(value));
    }

    public Task SaveGlobalWindowWidthAsync(int value)
    {
        return _settingsRepository.SaveAsync(GlobalWindowWidthKey, value);
    }

    public Task SaveGlobalWindowHeightAsync(int value)
    {
        return _settingsRepository.SaveAsync(GlobalWindowHeightKey, value);
    }

    public Task<string?> LoadMinecraftPathsJsonAsync()
    {
        return _settingsRepository.ReadAsync<string>(MinecraftPathsKey);
    }

    public Task SaveMinecraftPathsJsonAsync(string value)
    {
        return _settingsRepository.SaveAsync(MinecraftPathsKey, value);
    }

    public async Task<string> ResolveCurrentMinecraftPathAsync()
    {
        return await _settingsRepository.ReadAsync<string>(MinecraftPathKey)
            ?? _fileService.GetMinecraftDataPath();
    }

    public Task SaveMinecraftPathAsync(string value)
    {
        _fileService.SetMinecraftDataPath(value);
        return _settingsRepository.SaveAsync(MinecraftPathKey, value);
    }
}
