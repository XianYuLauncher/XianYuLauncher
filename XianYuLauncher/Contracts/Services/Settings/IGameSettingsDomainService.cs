using System.Collections.Generic;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

/// <summary>
/// Phase 3: 游戏设置领域服务（Java、目录、内存、分辨率、实时日志）。
/// </summary>
public sealed class GameGlobalLaunchSettingsState
{
	public bool AutoMemoryAllocation { get; init; } = true;

	public double InitialHeapMemory { get; init; } = 6.0;

	public double MaximumHeapMemory { get; init; } = 12.0;

	public string CustomJvmArguments { get; init; } = string.Empty;

	public string GarbageCollectorMode { get; init; } = XianYuLauncher.Core.Helpers.GarbageCollectorModeHelper.Auto;

	public int WindowWidth { get; init; } = 1280;

	public int WindowHeight { get; init; } = 720;
}

public interface IGameSettingsDomainService
{
	Task<bool> LoadEnableRealTimeLogsAsync();

	Task SaveEnableRealTimeLogsAsync(bool value);

	Task<bool> LoadEnableVersionIsolationAsync();

	Task SaveEnableVersionIsolationAsync(bool value);

	Task<string?> LoadGameIsolationModeAsync();

	Task SaveGameIsolationModeAsync(string value);

	Task<string?> LoadCustomGameDirectoryAsync();

	Task SaveCustomGameDirectoryAsync(string value);

	Task<string?> LoadJavaSelectionModeAsync();

	Task SaveJavaSelectionModeAsync(string value);

	Task<string?> LoadJavaPathAsync();

	Task SaveJavaPathAsync(string value);

	Task SaveSelectedJavaVersionAsync(string value);

	Task ClearJavaSelectionAsync();

	Task<IReadOnlyList<XianYuLauncher.Core.Models.JavaVersion>?> LoadJavaVersionsAsync();

	Task SaveJavaVersionsAsync(IReadOnlyList<XianYuLauncher.Core.Models.JavaVersion> versions);

	Task<GameGlobalLaunchSettingsState> LoadGlobalLaunchSettingsAsync();

	Task SaveGlobalAutoMemoryAllocationAsync(bool value);

	Task SaveGlobalInitialHeapMemoryAsync(double value);

	Task SaveGlobalMaximumHeapMemoryAsync(double value);

	Task SaveGlobalCustomJvmArgumentsAsync(string value);

	Task SaveGlobalGarbageCollectorModeAsync(string value);

	Task SaveGlobalWindowWidthAsync(int value);

	Task SaveGlobalWindowHeightAsync(int value);

	Task<string?> LoadMinecraftPathsJsonAsync();

	Task SaveMinecraftPathsJsonAsync(string value);

	Task<string> ResolveCurrentMinecraftPathAsync();

	Task SaveMinecraftPathAsync(string value);
}
