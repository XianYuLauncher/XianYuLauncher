using System.Globalization;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Features.ErrorAnalysis.Models;
using XianYuLauncher.Features.VersionManagement.Services;
using XianYuLauncher.Models.VersionManagement;
using XianYuLauncher.ViewModels;

namespace XianYuLauncher.Features.ErrorAnalysis.Services;

public interface IAgentSettingsWriteService
{
    Task<AgentToolExecutionResult> PrepareGlobalLaunchSettingsPatchAsync(AgentGlobalLaunchSettingsPatchRequest request, CancellationToken cancellationToken);

    Task<string> ExecuteGlobalLaunchSettingsPatchAsync(AgentActionProposal proposal, CancellationToken cancellationToken);

    Task<AgentToolExecutionResult> PrepareInstanceLaunchSettingsPatchAsync(AgentInstanceLaunchSettingsPatchRequest request, CancellationToken cancellationToken);

    Task<string> ExecuteInstanceLaunchSettingsPatchAsync(AgentActionProposal proposal, CancellationToken cancellationToken);
}

public sealed class AgentSettingsWriteService : IAgentSettingsWriteService
{
    private const string GlobalJavaSelectionModeParameterKey = "java_selection_mode";
    private const string GlobalSelectedJavaPathParameterKey = "selected_java_path";
    private const string GlobalCustomJvmArgumentsParameterKey = "custom_jvm_arguments";
    private const string GlobalGarbageCollectorModeParameterKey = "garbage_collector_mode";
    private const string GlobalAutoMemoryAllocationParameterKey = "auto_memory_allocation";
    private const string GlobalInitialHeapMemoryParameterKey = "initial_heap_memory_gb";
    private const string GlobalMaximumHeapMemoryParameterKey = "maximum_heap_memory_gb";
    private const string GlobalWindowWidthParameterKey = "window_width";
    private const string GlobalWindowHeightParameterKey = "window_height";
    private const string GlobalGameDirectoryModeParameterKey = "game_directory_mode";
    private const string GlobalCustomGameDirectoryPathParameterKey = "custom_game_directory_path";
    private const string InstanceUseGlobalJavaSettingParameterKey = "use_global_java_setting";
    private const string InstanceJavaPathParameterKey = "java_path";
    private const string InstanceCustomJvmArgumentsParameterKey = "custom_jvm_arguments";
    private const string InstanceGarbageCollectorModeParameterKey = "garbage_collector_mode";
    private const string InstanceOverrideMemoryParameterKey = "override_memory";
    private const string InstanceAutoMemoryAllocationParameterKey = "auto_memory_allocation";
    private const string InstanceInitialHeapMemoryParameterKey = "initial_heap_memory_gb";
    private const string InstanceMaximumHeapMemoryParameterKey = "maximum_heap_memory_gb";
    private const string InstanceOverrideResolutionParameterKey = "override_resolution";
    private const string InstanceWindowWidthParameterKey = "window_width";
    private const string InstanceWindowHeightParameterKey = "window_height";
    private const string InstanceGameDirectoryModeParameterKey = "game_directory_mode";
    private const string InstanceCustomGameDirectoryPathParameterKey = "custom_game_directory_path";
    private const string InstanceUseGlobalSettingsOverallParameterKey = "use_global_settings_overall";

    private readonly IGameSettingsDomainService _gameSettingsDomainService;
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IVersionSettingsOrchestrator _versionSettingsOrchestrator;
    private readonly IAgentSettingsActionProposalService _proposalService;

    private const double MinimumMemoryGb = 1;
    private const double MaximumMemoryGb = 64;
    private const int MinimumWindowWidth = 800;
    private const int MaximumWindowWidth = 4096;
    private const int MinimumWindowHeight = 600;
    private const int MaximumWindowHeight = 2160;
    private const string GameDirectoryModeDefault = "Default";
    private const string GameDirectoryModeVersionIsolation = "VersionIsolation";
    private const string GameDirectoryModeCustom = "Custom";
    private const string InstanceGameDirectoryFollowGlobalToken = "__FOLLOW_GLOBAL__";

    public AgentSettingsWriteService(
        IGameSettingsDomainService gameSettingsDomainService,
        IJavaRuntimeService javaRuntimeService,
        IMinecraftVersionService minecraftVersionService,
        IVersionInfoService versionInfoService,
        IVersionSettingsOrchestrator versionSettingsOrchestrator,
        IAgentSettingsActionProposalService proposalService)
    {
        _gameSettingsDomainService = gameSettingsDomainService;
        _javaRuntimeService = javaRuntimeService;
        _minecraftVersionService = minecraftVersionService;
        _versionInfoService = versionInfoService;
        _versionSettingsOrchestrator = versionSettingsOrchestrator;
        _proposalService = proposalService;
    }

    public async Task<AgentToolExecutionResult> PrepareGlobalLaunchSettingsPatchAsync(AgentGlobalLaunchSettingsPatchRequest request, CancellationToken cancellationToken)
    {
        var currentGlobalLaunchSettings = await _gameSettingsDomainService.LoadGlobalLaunchSettingsAsync();
        var currentMode = NormalizeJavaSelectionMode(await _gameSettingsDomainService.LoadJavaSelectionModeAsync());
        var currentSelectedJavaPath = NullIfWhiteSpace(await _gameSettingsDomainService.LoadJavaPathAsync());
        var currentCustomJvmArguments = NormalizeJvmArguments(currentGlobalLaunchSettings.CustomJvmArguments);
        var currentGarbageCollectorMode = GarbageCollectorModeHelper.Normalize(currentGlobalLaunchSettings.GarbageCollectorMode);
        var currentAutoMemoryAllocation = currentGlobalLaunchSettings.AutoMemoryAllocation;
        var currentInitialHeapMemory = currentGlobalLaunchSettings.InitialHeapMemory;
        var currentMaximumHeapMemory = currentGlobalLaunchSettings.MaximumHeapMemory;
        var currentWindowWidth = currentGlobalLaunchSettings.WindowWidth;
        var currentWindowHeight = currentGlobalLaunchSettings.WindowHeight;
        var currentGameDirectoryMode = ResolveEffectiveGlobalGameDirectoryMode(
            await _gameSettingsDomainService.LoadGameIsolationModeAsync(),
            await _gameSettingsDomainService.LoadEnableVersionIsolationAsync());
        var currentCustomGameDirectoryPath = NullIfWhiteSpace(await _gameSettingsDomainService.LoadCustomGameDirectoryAsync());
        var explicitMode = NormalizeRequestedJavaSelectionMode(request.JavaSelectionMode, out var modeErrorMessage);
        if (!string.IsNullOrWhiteSpace(modeErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(modeErrorMessage);
        }

        var explicitGarbageCollectorMode = NormalizeRequestedGarbageCollectorMode(request.GarbageCollectorMode, out var garbageCollectorModeErrorMessage);
        if (!string.IsNullOrWhiteSpace(garbageCollectorModeErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(garbageCollectorModeErrorMessage);
        }

        var explicitGameDirectoryMode = NormalizeRequestedGlobalGameDirectoryMode(request.GameDirectoryMode, out var gameDirectoryModeErrorMessage);
        if (!string.IsNullOrWhiteSpace(gameDirectoryModeErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(gameDirectoryModeErrorMessage);
        }

        var clearSelectedJava = request.ClearSelectedJava;
        if (clearSelectedJava
            && (!string.IsNullOrWhiteSpace(request.SelectedJavaId) || !string.IsNullOrWhiteSpace(request.SelectedJavaPath)))
        {
            return AgentToolExecutionResult.FromMessage("clear_selected_java 与 selected_java_id/selected_java_path 不能同时使用。");
        }

        var hasSelectedJavaRequest = !string.IsNullOrWhiteSpace(request.SelectedJavaId)
            || !string.IsNullOrWhiteSpace(request.SelectedJavaPath);
        var hasJvmSettingsRequest = request.HasCustomJvmArguments || explicitGarbageCollectorMode != null;
        var hasMemorySettingsRequest = request.AutoMemoryAllocation != null
            || request.InitialHeapMemoryGb.HasValue
            || request.MaximumHeapMemoryGb.HasValue;
        var hasResolutionSettingsRequest = request.WindowWidth.HasValue
            || request.WindowHeight.HasValue;
        var hasGameDirectorySettingsRequest = explicitGameDirectoryMode != null || request.HasCustomGameDirectoryPath;
        if (explicitMode == null
            && !clearSelectedJava
            && !hasSelectedJavaRequest
            && !hasJvmSettingsRequest
            && !hasMemorySettingsRequest
            && !hasResolutionSettingsRequest
            && !hasGameDirectorySettingsRequest)
        {
            return AgentToolExecutionResult.FromMessage("至少需要提供一个变更字段，例如 java_selection_mode、selected_java_id、selected_java_path、clear_selected_java、custom_jvm_arguments、garbage_collector_mode、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb、window_width、window_height、game_directory_mode 或 custom_game_directory_path。调用前建议先使用 getGlobalLaunchSettings 和 checkJavaVersions。");
        }

        var knownJavaVersions = await LoadKnownJavaVersionsAsync(cancellationToken);
        ResolvedJavaSelection? requestedJava = null;
        if (hasSelectedJavaRequest)
        {
            var resolution = await ResolveJavaSelectionAsync(
                currentSelectedJavaPath,
                request.SelectedJavaId,
                request.SelectedJavaPath,
                knownJavaVersions,
                cancellationToken);
            if (!resolution.Success)
            {
                return AgentToolExecutionResult.FromMessage(resolution.ErrorMessage);
            }

            knownJavaVersions = resolution.KnownJavaVersions.ToList();
            requestedJava = resolution.Selection;
        }

        var finalMode = explicitMode ?? currentMode;
        var finalSelectedJavaPath = currentSelectedJavaPath;
        var finalCustomJvmArguments = request.HasCustomJvmArguments
            ? NormalizeJvmArguments(request.CustomJvmArguments)
            : currentCustomJvmArguments;
        var finalGarbageCollectorMode = explicitGarbageCollectorMode ?? currentGarbageCollectorMode;
        var finalAutoMemoryAllocation = request.AutoMemoryAllocation ?? currentAutoMemoryAllocation;
        var finalInitialHeapMemory = request.InitialHeapMemoryGb ?? currentInitialHeapMemory;
        var finalMaximumHeapMemory = request.MaximumHeapMemoryGb ?? currentMaximumHeapMemory;
        var finalWindowWidth = request.WindowWidth ?? currentWindowWidth;
        var finalWindowHeight = request.WindowHeight ?? currentWindowHeight;
        var finalGameDirectoryMode = explicitGameDirectoryMode ?? currentGameDirectoryMode;
        var finalCustomGameDirectoryPath = request.HasCustomGameDirectoryPath
            ? NullIfWhiteSpace(request.CustomGameDirectoryPath)
            : currentCustomGameDirectoryPath;

        if (requestedJava != null)
        {
            if (string.Equals(finalMode, JavaSelectionModeAuto, StringComparison.Ordinal))
            {
                if (string.Equals(explicitMode, JavaSelectionModeAuto, StringComparison.Ordinal))
                {
                    return AgentToolExecutionResult.FromMessage("当 java_selection_mode=auto 时，不能同时指定 selected_java_id 或 selected_java_path。若要指定全局 Java，请改为 manual，或省略 java_selection_mode 让工具自动切到手动模式。");
                }

                finalMode = JavaSelectionModeManual;
            }

            finalSelectedJavaPath = requestedJava.Path;
        }

        if (clearSelectedJava)
        {
            finalSelectedJavaPath = null;
            if (string.Equals(finalMode, JavaSelectionModeManual, StringComparison.Ordinal))
            {
                if (string.Equals(explicitMode, JavaSelectionModeManual, StringComparison.Ordinal))
                {
                    return AgentToolExecutionResult.FromMessage("手动模式必须保留一个可用的全局 Java。若要清空手动选择，请切换到 auto，或省略 java_selection_mode 让工具自动回到自动模式。");
                }

                finalMode = JavaSelectionModeAuto;
            }
        }

        if (string.Equals(finalMode, JavaSelectionModeManual, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(finalSelectedJavaPath))
        {
            return AgentToolExecutionResult.FromMessage("手动模式下必须指定一个全局 Java。请提供 selected_java_id / selected_java_path，或改为 auto。");
        }

        if (request.InitialHeapMemoryGb.HasValue)
        {
            var validationMessage = ValidateMemoryValue("initial_heap_memory_gb", request.InitialHeapMemoryGb.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (request.MaximumHeapMemoryGb.HasValue)
        {
            var validationMessage = ValidateMemoryValue("maximum_heap_memory_gb", request.MaximumHeapMemoryGb.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (finalAutoMemoryAllocation && (request.InitialHeapMemoryGb.HasValue || request.MaximumHeapMemoryGb.HasValue))
        {
            return AgentToolExecutionResult.FromMessage("当 auto_memory_allocation=true 时，不能同时设置 initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改手动内存，请先将 auto_memory_allocation 设为 false。");
        }

        if (!finalAutoMemoryAllocation && finalInitialHeapMemory > finalMaximumHeapMemory)
        {
            return AgentToolExecutionResult.FromMessage("initial_heap_memory_gb 不能大于 maximum_heap_memory_gb。");
        }

        if (request.WindowWidth.HasValue)
        {
            var validationMessage = ValidateWindowWidth(request.WindowWidth.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (request.WindowHeight.HasValue)
        {
            var validationMessage = ValidateWindowHeight(request.WindowHeight.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (hasGameDirectorySettingsRequest)
        {
            if (!string.Equals(finalGameDirectoryMode, GameDirectoryModeCustom, StringComparison.Ordinal)
                && request.HasCustomGameDirectoryPath
                && !string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath))
            {
                    return AgentToolExecutionResult.FromMessage("custom_game_directory_path 仅在 game_directory_mode=custom 时允许设置。若只想清空已保存的自定义目录，可传空字符串。");
            }

            if (string.Equals(finalGameDirectoryMode, GameDirectoryModeCustom, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath)
                    || !Path.IsPathFullyQualified(finalCustomGameDirectoryPath))
                {
                        return AgentToolExecutionResult.FromMessage("当 game_directory_mode=custom 时，必须提供 custom_game_directory_path，且它必须是绝对路径。");
                }
            }
            else if (request.HasCustomGameDirectoryPath
                && !string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath)
                && !Path.IsPathFullyQualified(finalCustomGameDirectoryPath))
            {
                    return AgentToolExecutionResult.FromMessage("custom_game_directory_path 必须是绝对路径。若要清空已保存的自定义目录，请传空字符串。");
            }
        }

        List<AgentSettingsFieldChange> changes = [];
        if (!string.Equals(currentMode, finalMode, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "java_selection_mode",
                DisplayName = "Java 选择方式",
                OldValue = DescribeGlobalJavaSelectionMode(currentMode),
                NewValue = DescribeGlobalJavaSelectionMode(finalMode),
            });
        }

        if (!PathEquals(currentSelectedJavaPath, finalSelectedJavaPath))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "selected_java_path",
                DisplayName = "当前全局 Java",
                OldValue = DescribeSelectedJava(currentSelectedJavaPath, knownJavaVersions),
                NewValue = DescribeSelectedJava(finalSelectedJavaPath, knownJavaVersions),
            });
        }

        if (!string.Equals(currentCustomJvmArguments, finalCustomJvmArguments, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "custom_jvm_arguments",
                DisplayName = "JVM 参数",
                OldValue = DescribeJvmArguments(currentCustomJvmArguments),
                NewValue = DescribeJvmArguments(finalCustomJvmArguments),
            });
        }

        if (!string.Equals(currentGarbageCollectorMode, finalGarbageCollectorMode, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "garbage_collector_mode",
                DisplayName = "GC 模式",
                OldValue = currentGarbageCollectorMode,
                NewValue = finalGarbageCollectorMode,
            });
        }

        if (currentAutoMemoryAllocation != finalAutoMemoryAllocation)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "auto_memory_allocation",
                DisplayName = "内存分配方式",
                OldValue = DescribeMemoryAllocationMode(currentAutoMemoryAllocation),
                NewValue = DescribeMemoryAllocationMode(finalAutoMemoryAllocation),
            });
        }

        if (!DoubleEquals(currentInitialHeapMemory, finalInitialHeapMemory))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "initial_heap_memory_gb",
                DisplayName = "初始内存",
                OldValue = DescribeMemoryAmount(currentInitialHeapMemory),
                NewValue = DescribeMemoryAmount(finalInitialHeapMemory),
            });
        }

        if (!DoubleEquals(currentMaximumHeapMemory, finalMaximumHeapMemory))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "maximum_heap_memory_gb",
                DisplayName = "最大内存",
                OldValue = DescribeMemoryAmount(currentMaximumHeapMemory),
                NewValue = DescribeMemoryAmount(finalMaximumHeapMemory),
            });
        }

        if (currentWindowWidth != finalWindowWidth)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "window_width",
                DisplayName = "窗口宽度",
                OldValue = DescribeWindowSize(currentWindowWidth),
                NewValue = DescribeWindowSize(finalWindowWidth),
            });
        }

        if (currentWindowHeight != finalWindowHeight)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "window_height",
                DisplayName = "窗口高度",
                OldValue = DescribeWindowSize(currentWindowHeight),
                NewValue = DescribeWindowSize(finalWindowHeight),
            });
        }

        if (!string.Equals(currentGameDirectoryMode, finalGameDirectoryMode, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "game_directory_mode",
                DisplayName = "游戏目录模式",
                OldValue = DescribeGlobalGameDirectoryMode(currentGameDirectoryMode),
                NewValue = DescribeGlobalGameDirectoryMode(finalGameDirectoryMode),
            });
        }

        if (!PathEquals(currentCustomGameDirectoryPath, finalCustomGameDirectoryPath))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "custom_game_directory_path",
                DisplayName = "自定义游戏目录",
                OldValue = DescribeGameDirectoryPath(currentCustomGameDirectoryPath),
                NewValue = DescribeGameDirectoryPath(finalCustomGameDirectoryPath),
            });
        }

        if (changes.Count == 0)
        {
            return AgentToolExecutionResult.FromMessage("全局 Java/JVM/GC/内存/分辨率/游戏目录设置未发生变化。");
        }

        var proposal = _proposalService.CreateProposal(
            PatchGlobalLaunchSettingsToolHandler.ToolNameValue,
            "应用全局启动设置",
            new AgentSettingsActionProposalPayload
            {
                Scope = AgentSettingsProposalScopes.Global,
                Changes = changes,
            });

        proposal.Parameters[GlobalJavaSelectionModeParameterKey] = finalMode;
        proposal.Parameters[GlobalSelectedJavaPathParameterKey] = finalSelectedJavaPath ?? string.Empty;
        proposal.Parameters[GlobalCustomJvmArgumentsParameterKey] = finalCustomJvmArguments;
        proposal.Parameters[GlobalGarbageCollectorModeParameterKey] = finalGarbageCollectorMode;
        proposal.Parameters[GlobalAutoMemoryAllocationParameterKey] = finalAutoMemoryAllocation.ToString();
        proposal.Parameters[GlobalInitialHeapMemoryParameterKey] = finalInitialHeapMemory.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[GlobalMaximumHeapMemoryParameterKey] = finalMaximumHeapMemory.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[GlobalWindowWidthParameterKey] = finalWindowWidth.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[GlobalWindowHeightParameterKey] = finalWindowHeight.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[GlobalGameDirectoryModeParameterKey] = finalGameDirectoryMode;
        proposal.Parameters[GlobalCustomGameDirectoryPathParameterKey] = finalCustomGameDirectoryPath ?? string.Empty;

        return AgentToolExecutionResult.FromActionProposal(proposal.DisplayMessage, proposal);
    }

    public async Task<string> ExecuteGlobalLaunchSettingsPatchAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        if (!proposal.Parameters.TryGetValue(GlobalJavaSelectionModeParameterKey, out var finalMode)
            || string.IsNullOrWhiteSpace(finalMode))
        {
            return "缺少 java_selection_mode 参数。";
        }

        finalMode = NormalizeJavaSelectionMode(finalMode);
        proposal.Parameters.TryGetValue(GlobalSelectedJavaPathParameterKey, out var rawSelectedJavaPath);
        var finalSelectedJavaPath = NullIfWhiteSpace(rawSelectedJavaPath);
        if (string.Equals(finalMode, JavaSelectionModeManual, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(finalSelectedJavaPath))
        {
            return "手动模式下必须指定一个全局 Java。";
        }

        proposal.Parameters.TryGetValue(GlobalCustomJvmArgumentsParameterKey, out var rawCustomJvmArguments);
        var finalCustomJvmArguments = NormalizeJvmArguments(rawCustomJvmArguments);
        proposal.Parameters.TryGetValue(GlobalGarbageCollectorModeParameterKey, out var rawGarbageCollectorMode);
        var finalGarbageCollectorMode = GarbageCollectorModeHelper.Normalize(rawGarbageCollectorMode);
        proposal.Parameters.TryGetValue(GlobalAutoMemoryAllocationParameterKey, out var rawAutoMemoryAllocation);
        var finalAutoMemoryAllocation = bool.TryParse(rawAutoMemoryAllocation, out var parsedAutoMemoryAllocation)
            && parsedAutoMemoryAllocation;
        proposal.Parameters.TryGetValue(GlobalInitialHeapMemoryParameterKey, out var rawInitialHeapMemory);
        var finalInitialHeapMemory = ParseStoredDouble(rawInitialHeapMemory);
        proposal.Parameters.TryGetValue(GlobalMaximumHeapMemoryParameterKey, out var rawMaximumHeapMemory);
        var finalMaximumHeapMemory = ParseStoredDouble(rawMaximumHeapMemory);
        proposal.Parameters.TryGetValue(GlobalWindowWidthParameterKey, out var rawWindowWidth);
        var finalWindowWidth = ParseStoredInt32(rawWindowWidth);
        proposal.Parameters.TryGetValue(GlobalWindowHeightParameterKey, out var rawWindowHeight);
        var finalWindowHeight = ParseStoredInt32(rawWindowHeight);
        if (!proposal.Parameters.TryGetValue(GlobalGameDirectoryModeParameterKey, out var rawGameDirectoryMode)
            || string.IsNullOrWhiteSpace(rawGameDirectoryMode))
        {
            return "缺少 game_directory_mode 参数。";
        }

        var finalGameDirectoryMode = ResolveEffectiveGlobalGameDirectoryMode(rawGameDirectoryMode, legacyEnableVersionIsolation: true);
        proposal.Parameters.TryGetValue(GlobalCustomGameDirectoryPathParameterKey, out var rawCustomGameDirectoryPath);
        var finalCustomGameDirectoryPath = NullIfWhiteSpace(rawCustomGameDirectoryPath);

        if (string.Equals(finalMode, JavaSelectionModeManual, StringComparison.Ordinal))
        {
            var ensureResult = await EnsureJavaPresentInKnownListAsync(finalSelectedJavaPath!, cancellationToken);
            if (!ensureResult.Success)
            {
                return ensureResult.ErrorMessage;
            }

            if (ensureResult.KnownJavaVersionsChanged)
            {
                await _gameSettingsDomainService.SaveJavaVersionsAsync(ensureResult.KnownJavaVersions);
            }
        }

        await _gameSettingsDomainService.SaveJavaSelectionModeAsync(ToStoredJavaSelectionMode(finalMode));
        if (string.IsNullOrWhiteSpace(finalSelectedJavaPath))
        {
            await _gameSettingsDomainService.ClearJavaSelectionAsync();
        }
        else
        {
            await _gameSettingsDomainService.SaveSelectedJavaVersionAsync(finalSelectedJavaPath);
        }

        await _gameSettingsDomainService.SaveGlobalCustomJvmArgumentsAsync(finalCustomJvmArguments);
        await _gameSettingsDomainService.SaveGlobalGarbageCollectorModeAsync(finalGarbageCollectorMode);
        await _gameSettingsDomainService.SaveGlobalAutoMemoryAllocationAsync(finalAutoMemoryAllocation);
        await _gameSettingsDomainService.SaveGlobalInitialHeapMemoryAsync(finalInitialHeapMemory);
        await _gameSettingsDomainService.SaveGlobalMaximumHeapMemoryAsync(finalMaximumHeapMemory);
        await _gameSettingsDomainService.SaveGlobalWindowWidthAsync(finalWindowWidth);
        await _gameSettingsDomainService.SaveGlobalWindowHeightAsync(finalWindowHeight);
        await _gameSettingsDomainService.SaveGameIsolationModeAsync(finalGameDirectoryMode);
        await _gameSettingsDomainService.SaveEnableVersionIsolationAsync(ShouldUseLegacyVersionIsolation(finalGameDirectoryMode));
        await _gameSettingsDomainService.SaveCustomGameDirectoryAsync(finalCustomGameDirectoryPath ?? string.Empty);

        return $"已更新全局启动设置：{BuildChangeSummary(proposal)}。";
    }

    public async Task<AgentToolExecutionResult> PrepareInstanceLaunchSettingsPatchAsync(AgentInstanceLaunchSettingsPatchRequest request, CancellationToken cancellationToken)
    {
        var targetVersion = await ResolveTargetVersionAsync(request.TargetVersionName, request.TargetVersionPath, cancellationToken);
        if (!targetVersion.Success)
        {
            return AgentToolExecutionResult.FromMessage(targetVersion.ErrorMessage);
        }

        var hasJavaRequest = !string.IsNullOrWhiteSpace(request.JavaId)
            || !string.IsNullOrWhiteSpace(request.JavaPath);
        var explicitGarbageCollectorMode = NormalizeRequestedGarbageCollectorMode(request.GarbageCollectorMode, out var garbageCollectorModeErrorMessage);
        if (!string.IsNullOrWhiteSpace(garbageCollectorModeErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(garbageCollectorModeErrorMessage);
        }

        var hasJvmSettingsRequest = request.HasCustomJvmArguments || explicitGarbageCollectorMode != null;
        var hasMemorySettingsRequest = request.AutoMemoryAllocation != null
            || request.InitialHeapMemoryGb.HasValue
            || request.MaximumHeapMemoryGb.HasValue;
        var hasResolutionSettingsRequest = request.WindowWidth.HasValue
            || request.WindowHeight.HasValue;
        var explicitGameDirectoryModeToken = NormalizeRequestedInstanceGameDirectoryMode(request.GameDirectoryMode, out var gameDirectoryModeErrorMessage);
        if (!string.IsNullOrWhiteSpace(gameDirectoryModeErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(gameDirectoryModeErrorMessage);
        }

        var hasGameDirectorySettingsRequest = explicitGameDirectoryModeToken != null || request.HasCustomGameDirectoryPath;
        var hasLocalGameDirectoryValueRequest = HasLocalGameDirectoryValueRequest(explicitGameDirectoryModeToken, request.HasCustomGameDirectoryPath);
        var legacyRequestedUseGlobalSettingsOverall = InferUseGlobalSettingsOverallFromLegacyInstanceFlags(
            request.UseGlobalJavaSetting,
            request.OverrideMemory,
            request.OverrideResolution,
            out var legacyGlobalSettingsErrorMessage);
        if (!string.IsNullOrWhiteSpace(legacyGlobalSettingsErrorMessage))
        {
            return AgentToolExecutionResult.FromMessage(legacyGlobalSettingsErrorMessage);
        }

        var requestedUseGlobalSettingsOverall = request.UseGlobalSettingsOverall;
        if (requestedUseGlobalSettingsOverall.HasValue
            && legacyRequestedUseGlobalSettingsOverall.HasValue
            && requestedUseGlobalSettingsOverall.Value != legacyRequestedUseGlobalSettingsOverall.Value)
        {
            return AgentToolExecutionResult.FromMessage("use_global_java_setting / override_memory / override_resolution 已废弃，且与 use_global_settings_overall 冲突。请仅使用 use_global_settings_overall 表达实例是否跟随全局。");
        }

        var effectiveRequestedUseGlobalSettingsOverall = requestedUseGlobalSettingsOverall ?? legacyRequestedUseGlobalSettingsOverall;
        if (!effectiveRequestedUseGlobalSettingsOverall.HasValue
            && (hasJavaRequest || hasJvmSettingsRequest || hasMemorySettingsRequest || hasResolutionSettingsRequest || hasLocalGameDirectoryValueRequest))
        {
            effectiveRequestedUseGlobalSettingsOverall = false;
        }

        if (!effectiveRequestedUseGlobalSettingsOverall.HasValue
            && !hasJavaRequest
            && !hasJvmSettingsRequest
            && !hasMemorySettingsRequest
            && !hasResolutionSettingsRequest
            && !hasGameDirectorySettingsRequest)
        {
            return AgentToolExecutionResult.FromMessage("至少需要提供一个实例级变更字段，例如 use_global_settings_overall、java_id、java_path、custom_jvm_arguments、garbage_collector_mode、auto_memory_allocation、initial_heap_memory_gb、maximum_heap_memory_gb、window_width、window_height、game_directory_mode 或 custom_game_directory_path。调用前建议先使用 get_instances、getVersionConfig 和 checkJavaVersions。");
        }

        if (effectiveRequestedUseGlobalSettingsOverall == true)
        {
            if (hasJavaRequest || hasJvmSettingsRequest)
            {
                return AgentToolExecutionResult.FromMessage("当 use_global_settings_overall=true 时，不能同时设置 java_id、java_path、custom_jvm_arguments 或 garbage_collector_mode。若要修改实例本地 Java/JVM/GC，请将 use_global_settings_overall 设为 false，或直接省略该字段让工具自动关闭总开关。");
            }

            if (hasMemorySettingsRequest)
            {
                return AgentToolExecutionResult.FromMessage("当 use_global_settings_overall=true 时，不能同时设置 auto_memory_allocation、initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改实例本地内存，请将 use_global_settings_overall 设为 false，或直接省略该字段让工具自动关闭总开关。");
            }

            if (hasResolutionSettingsRequest)
            {
                return AgentToolExecutionResult.FromMessage("当 use_global_settings_overall=true 时，不能同时设置 window_width 或 window_height。若要修改实例本地分辨率，请将 use_global_settings_overall 设为 false，或直接省略该字段让工具自动关闭总开关。");
            }

            if (hasGameDirectorySettingsRequest)
            {
                return AgentToolExecutionResult.FromMessage("当 use_global_settings_overall=true 时，不能同时修改 game_directory_mode 或 custom_game_directory_path。若要调整实例游戏目录，请先关闭总开关。");
            }
        }

        var currentConfig = await _versionInfoService.GetFullVersionInfoAsync(
            targetVersion.VersionName,
            targetVersion.VersionDirectoryPath,
            preferCache: true);
        cancellationToken.ThrowIfCancellationRequested();

        var currentUseGlobalJavaSetting = currentConfig.UseGlobalJavaSetting;
        var currentJavaPath = NullIfWhiteSpace(currentConfig.JavaPath);
        var currentCustomJvmArguments = NormalizeJvmArguments(currentConfig.CustomJvmArguments);
        var currentGarbageCollectorMode = GarbageCollectorModeHelper.Normalize(currentConfig.GarbageCollectorMode);
        var currentOverrideMemory = currentConfig.OverrideMemory;
        var currentAutoMemoryAllocation = currentConfig.AutoMemoryAllocation;
        var currentInitialHeapMemory = currentConfig.InitialHeapMemory;
        var currentMaximumHeapMemory = currentConfig.MaximumHeapMemory;
        var currentOverrideResolution = currentConfig.OverrideResolution;
        var currentWindowWidth = currentConfig.WindowWidth;
        var currentWindowHeight = currentConfig.WindowHeight;
        var currentGameDirectoryMode = currentConfig.GameDirMode;
        var currentCustomGameDirectoryPath = NullIfWhiteSpace(currentConfig.GameDirCustomPath);
        var knownJavaVersions = await LoadKnownJavaVersionsAsync(cancellationToken);
        ResolvedJavaSelection? requestedJava = null;
        if (hasJavaRequest)
        {
            var resolution = await ResolveJavaSelectionAsync(
                currentJavaPath,
                request.JavaId,
                request.JavaPath,
                knownJavaVersions,
                cancellationToken);
            if (!resolution.Success)
            {
                return AgentToolExecutionResult.FromMessage(resolution.ErrorMessage);
            }

            knownJavaVersions = resolution.KnownJavaVersions.ToList();
            requestedJava = resolution.Selection;
        }

        var finalUseGlobalJavaSetting = effectiveRequestedUseGlobalSettingsOverall.HasValue
            ? effectiveRequestedUseGlobalSettingsOverall.Value
            : currentUseGlobalJavaSetting;
        var finalJavaPath = currentJavaPath;
        var finalCustomJvmArguments = request.HasCustomJvmArguments
            ? NormalizeJvmArguments(request.CustomJvmArguments)
            : currentCustomJvmArguments;
        var finalGarbageCollectorMode = explicitGarbageCollectorMode ?? currentGarbageCollectorMode;
        var finalOverrideMemory = effectiveRequestedUseGlobalSettingsOverall.HasValue
            ? !effectiveRequestedUseGlobalSettingsOverall.Value
            : currentOverrideMemory;
        var finalAutoMemoryAllocation = request.AutoMemoryAllocation ?? currentAutoMemoryAllocation;
        var finalInitialHeapMemory = request.InitialHeapMemoryGb ?? currentInitialHeapMemory;
        var finalMaximumHeapMemory = request.MaximumHeapMemoryGb ?? currentMaximumHeapMemory;
        var finalOverrideResolution = effectiveRequestedUseGlobalSettingsOverall.HasValue
            ? !effectiveRequestedUseGlobalSettingsOverall.Value
            : currentOverrideResolution;
        var finalWindowWidth = request.WindowWidth ?? currentWindowWidth;
        var finalWindowHeight = request.WindowHeight ?? currentWindowHeight;
        var finalGameDirectoryMode = explicitGameDirectoryModeToken switch
        {
            null => currentGameDirectoryMode,
            InstanceGameDirectoryFollowGlobalToken => null,
            _ => explicitGameDirectoryModeToken,
        };
        var finalCustomGameDirectoryPath = request.HasCustomGameDirectoryPath
            ? NullIfWhiteSpace(request.CustomGameDirectoryPath)
            : currentCustomGameDirectoryPath;
        if (requestedJava != null)
        {
            finalUseGlobalJavaSetting = false;
            finalJavaPath = requestedJava.Path;
        }

        var allowEmptyIndependentJavaPath = !hasJavaRequest
            && !UsesGlobalSettingsOverall(finalUseGlobalJavaSetting, finalOverrideMemory, finalOverrideResolution);
        if (!finalUseGlobalJavaSetting && string.IsNullOrWhiteSpace(finalJavaPath) && !allowEmptyIndependentJavaPath)
        {
            return AgentToolExecutionResult.FromMessage("当前实例将改为使用本地设置，但本地 Java 仍未配置。请提供 java_id 或 java_path，或将 use_global_settings_overall 设为 true。");
        }

        if (request.InitialHeapMemoryGb.HasValue)
        {
            var validationMessage = ValidateMemoryValue("initial_heap_memory_gb", request.InitialHeapMemoryGb.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (request.MaximumHeapMemoryGb.HasValue)
        {
            var validationMessage = ValidateMemoryValue("maximum_heap_memory_gb", request.MaximumHeapMemoryGb.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (!finalOverrideMemory && (request.AutoMemoryAllocation != null || request.InitialHeapMemoryGb.HasValue || request.MaximumHeapMemoryGb.HasValue))
        {
            return AgentToolExecutionResult.FromMessage("当前实例仍使用全局设置，不能同时设置 auto_memory_allocation、initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改实例本地内存，可将 use_global_settings_overall 设为 false，或直接省略该字段让工具自动关闭总开关。");
        }

        if (finalOverrideMemory && finalAutoMemoryAllocation && (request.InitialHeapMemoryGb.HasValue || request.MaximumHeapMemoryGb.HasValue))
        {
            return AgentToolExecutionResult.FromMessage("当 auto_memory_allocation=true 时，不能同时设置 initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改手动内存，请先将 auto_memory_allocation 设为 false。");
        }

        if (finalOverrideMemory && !finalAutoMemoryAllocation && finalInitialHeapMemory > finalMaximumHeapMemory)
        {
            return AgentToolExecutionResult.FromMessage("initial_heap_memory_gb 不能大于 maximum_heap_memory_gb。");
        }

        if (request.WindowWidth.HasValue)
        {
            var validationMessage = ValidateWindowWidth(request.WindowWidth.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (request.WindowHeight.HasValue)
        {
            var validationMessage = ValidateWindowHeight(request.WindowHeight.Value);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return AgentToolExecutionResult.FromMessage(validationMessage);
            }
        }

        if (!finalOverrideResolution && (request.WindowWidth.HasValue || request.WindowHeight.HasValue))
        {
            return AgentToolExecutionResult.FromMessage("当前实例仍使用全局设置，不能同时设置 window_width 或 window_height。若要修改实例本地分辨率，可将 use_global_settings_overall 设为 false，或直接省略该字段让工具自动关闭总开关。");
        }

        if (hasGameDirectorySettingsRequest)
        {
            if (string.IsNullOrWhiteSpace(finalGameDirectoryMode))
            {
                if (request.HasCustomGameDirectoryPath && !string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath))
                {
                        return AgentToolExecutionResult.FromMessage("当 game_directory_mode=follow_global 时，不能同时设置 custom_game_directory_path。若只想清空已保存的自定义目录，可传空字符串。");
                }
            }
            else
            {
                if (!string.Equals(finalGameDirectoryMode, GameDirectoryModeCustom, StringComparison.Ordinal)
                    && request.HasCustomGameDirectoryPath
                    && !string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath))
                {
                        return AgentToolExecutionResult.FromMessage("custom_game_directory_path 仅在 game_directory_mode=custom 时允许设置。若只想清空已保存的自定义目录，可传空字符串。");
                }

                if (string.Equals(finalGameDirectoryMode, GameDirectoryModeCustom, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath)
                        || !Path.IsPathFullyQualified(finalCustomGameDirectoryPath))
                    {
                            return AgentToolExecutionResult.FromMessage("当 game_directory_mode=custom 时，必须提供 custom_game_directory_path，且它必须是绝对路径。");
                    }
                }
                else if (request.HasCustomGameDirectoryPath
                    && !string.IsNullOrWhiteSpace(finalCustomGameDirectoryPath)
                    && !Path.IsPathFullyQualified(finalCustomGameDirectoryPath))
                {
                        return AgentToolExecutionResult.FromMessage("custom_game_directory_path 必须是绝对路径。若要清空已保存的自定义目录，请传空字符串。");
                }
            }
        }

        var currentUsesGlobalSettingsOverall = UsesGlobalSettingsOverall(currentUseGlobalJavaSetting, currentConfig.OverrideMemory, currentConfig.OverrideResolution);
        var finalUsesGlobalSettingsOverall = UsesGlobalSettingsOverall(finalUseGlobalJavaSetting, finalOverrideMemory, finalOverrideResolution);
        List<AgentSettingsFieldChange> changes = [];
        if (currentUsesGlobalSettingsOverall != finalUsesGlobalSettingsOverall)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "uses_global_settings_overall",
                DisplayName = "使用全局设置",
                OldValue = DescribeUseGlobalSettingsOverall(currentUsesGlobalSettingsOverall),
                NewValue = DescribeUseGlobalSettingsOverall(finalUsesGlobalSettingsOverall),
                SwitchesToFollowGlobal = finalUsesGlobalSettingsOverall,
                SwitchesToOverride = !finalUsesGlobalSettingsOverall,
            });
        }

        var oldJavaDisplay = DescribeSelectedJava(currentJavaPath, knownJavaVersions);
        var newJavaDisplay = DescribeSelectedJava(finalJavaPath, knownJavaVersions);
        if (!string.Equals(oldJavaDisplay, newJavaDisplay, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "java_path",
                DisplayName = "Java 路径",
                OldValue = oldJavaDisplay,
                NewValue = newJavaDisplay,
            });
        }

        if (!string.Equals(currentCustomJvmArguments, finalCustomJvmArguments, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "custom_jvm_arguments",
                DisplayName = "JVM 参数",
                OldValue = DescribeJvmArguments(currentCustomJvmArguments),
                NewValue = DescribeJvmArguments(finalCustomJvmArguments),
            });
        }

        if (!string.Equals(currentGarbageCollectorMode, finalGarbageCollectorMode, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "garbage_collector_mode",
                DisplayName = "GC 模式",
                OldValue = currentGarbageCollectorMode,
                NewValue = finalGarbageCollectorMode,
            });
        }

        if (currentAutoMemoryAllocation != finalAutoMemoryAllocation)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "auto_memory_allocation",
                DisplayName = "内存分配方式",
                OldValue = DescribeMemoryAllocationMode(currentAutoMemoryAllocation),
                NewValue = DescribeMemoryAllocationMode(finalAutoMemoryAllocation),
            });
        }

        if (!DoubleEquals(currentInitialHeapMemory, finalInitialHeapMemory))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "initial_heap_memory_gb",
                DisplayName = "初始内存",
                OldValue = DescribeMemoryAmount(currentInitialHeapMemory),
                NewValue = DescribeMemoryAmount(finalInitialHeapMemory),
            });
        }

        if (!DoubleEquals(currentMaximumHeapMemory, finalMaximumHeapMemory))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "maximum_heap_memory_gb",
                DisplayName = "最大内存",
                OldValue = DescribeMemoryAmount(currentMaximumHeapMemory),
                NewValue = DescribeMemoryAmount(finalMaximumHeapMemory),
            });
        }

        if (currentWindowWidth != finalWindowWidth)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "window_width",
                DisplayName = "窗口宽度",
                OldValue = DescribeWindowSize(currentWindowWidth),
                NewValue = DescribeWindowSize(finalWindowWidth),
            });
        }

        if (currentWindowHeight != finalWindowHeight)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "window_height",
                DisplayName = "窗口高度",
                OldValue = DescribeWindowSize(currentWindowHeight),
                NewValue = DescribeWindowSize(finalWindowHeight),
            });
        }

        if (!string.Equals(currentGameDirectoryMode, finalGameDirectoryMode, StringComparison.Ordinal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "game_directory_mode",
                DisplayName = "游戏目录模式",
                OldValue = DescribeInstanceGameDirectoryMode(currentGameDirectoryMode),
                NewValue = DescribeInstanceGameDirectoryMode(finalGameDirectoryMode),
                SwitchesToFollowGlobal = !string.IsNullOrWhiteSpace(currentGameDirectoryMode) && string.IsNullOrWhiteSpace(finalGameDirectoryMode),
                SwitchesToOverride = string.IsNullOrWhiteSpace(currentGameDirectoryMode) && !string.IsNullOrWhiteSpace(finalGameDirectoryMode),
            });
        }

        if (!PathEquals(currentCustomGameDirectoryPath, finalCustomGameDirectoryPath))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "custom_game_directory_path",
                DisplayName = "自定义游戏目录",
                OldValue = DescribeGameDirectoryPath(currentCustomGameDirectoryPath),
                NewValue = DescribeGameDirectoryPath(finalCustomGameDirectoryPath),
            });
        }

        if (changes.Count == 0)
        {
            return AgentToolExecutionResult.FromMessage($"实例 {targetVersion.VersionName} 的 Java/JVM/GC/内存/分辨率/游戏目录设置未发生变化。");
        }

        var proposal = _proposalService.CreateProposal(
            PatchInstanceLaunchSettingsToolHandler.ToolNameValue,
            $"更新 {targetVersion.VersionName} 的启动设置",
            new AgentSettingsActionProposalPayload
            {
                Scope = AgentSettingsProposalScopes.Instance,
                TargetVersionName = targetVersion.VersionName,
                TargetVersionPath = targetVersion.VersionDirectoryPath,
                Changes = changes,
            });

        proposal.Parameters[InstanceUseGlobalJavaSettingParameterKey] = finalUseGlobalJavaSetting.ToString();
        proposal.Parameters[InstanceJavaPathParameterKey] = finalJavaPath ?? string.Empty;
        proposal.Parameters[InstanceCustomJvmArgumentsParameterKey] = finalCustomJvmArguments;
        proposal.Parameters[InstanceGarbageCollectorModeParameterKey] = finalGarbageCollectorMode;
        proposal.Parameters[InstanceOverrideMemoryParameterKey] = finalOverrideMemory.ToString();
        proposal.Parameters[InstanceAutoMemoryAllocationParameterKey] = finalAutoMemoryAllocation.ToString();
        proposal.Parameters[InstanceInitialHeapMemoryParameterKey] = finalInitialHeapMemory.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[InstanceMaximumHeapMemoryParameterKey] = finalMaximumHeapMemory.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[InstanceOverrideResolutionParameterKey] = finalOverrideResolution.ToString();
        proposal.Parameters[InstanceWindowWidthParameterKey] = finalWindowWidth.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[InstanceWindowHeightParameterKey] = finalWindowHeight.ToString(CultureInfo.InvariantCulture);
        proposal.Parameters[InstanceGameDirectoryModeParameterKey] = finalGameDirectoryMode ?? string.Empty;
        proposal.Parameters[InstanceCustomGameDirectoryPathParameterKey] = finalCustomGameDirectoryPath ?? string.Empty;
        proposal.Parameters[InstanceUseGlobalSettingsOverallParameterKey] = finalUsesGlobalSettingsOverall.ToString();

        return AgentToolExecutionResult.FromActionProposal(proposal.DisplayMessage, proposal);
    }

    public async Task<string> ExecuteInstanceLaunchSettingsPatchAsync(AgentActionProposal proposal, CancellationToken cancellationToken)
    {
        var targetVersion = await ResolveTargetVersionAsync(
            proposal.Parameters.TryGetValue("target_version_name", out var targetVersionName) ? targetVersionName : null,
            proposal.Parameters.TryGetValue("target_version_path", out var targetVersionPath) ? targetVersionPath : null,
            cancellationToken);
        if (!targetVersion.Success)
        {
            return targetVersion.ErrorMessage;
        }

        if (!proposal.Parameters.TryGetValue(InstanceUseGlobalJavaSettingParameterKey, out var rawUseGlobalJavaSetting)
            || !bool.TryParse(rawUseGlobalJavaSetting, out var finalUseGlobalJavaSetting))
        {
            return "缺少 use_global_java_setting 参数。";
        }

        proposal.Parameters.TryGetValue(InstanceJavaPathParameterKey, out var rawJavaPath);
        var finalJavaPath = NullIfWhiteSpace(rawJavaPath);
        proposal.Parameters.TryGetValue(InstanceCustomJvmArgumentsParameterKey, out var rawCustomJvmArguments);
        var finalCustomJvmArguments = NormalizeJvmArguments(rawCustomJvmArguments);
        proposal.Parameters.TryGetValue(InstanceGarbageCollectorModeParameterKey, out var rawGarbageCollectorMode);
        var finalGarbageCollectorMode = GarbageCollectorModeHelper.Normalize(rawGarbageCollectorMode);
        proposal.Parameters.TryGetValue(InstanceOverrideMemoryParameterKey, out var rawOverrideMemory);
        var finalOverrideMemory = bool.TryParse(rawOverrideMemory, out var parsedOverrideMemory)
            && parsedOverrideMemory;
        proposal.Parameters.TryGetValue(InstanceAutoMemoryAllocationParameterKey, out var rawAutoMemoryAllocation);
        var finalAutoMemoryAllocation = bool.TryParse(rawAutoMemoryAllocation, out var parsedAutoMemoryAllocation)
            && parsedAutoMemoryAllocation;
        proposal.Parameters.TryGetValue(InstanceInitialHeapMemoryParameterKey, out var rawInitialHeapMemory);
        var finalInitialHeapMemory = ParseStoredDouble(rawInitialHeapMemory);
        proposal.Parameters.TryGetValue(InstanceMaximumHeapMemoryParameterKey, out var rawMaximumHeapMemory);
        var finalMaximumHeapMemory = ParseStoredDouble(rawMaximumHeapMemory);
        proposal.Parameters.TryGetValue(InstanceOverrideResolutionParameterKey, out var rawOverrideResolution);
        var finalOverrideResolution = bool.TryParse(rawOverrideResolution, out var parsedOverrideResolution)
            && parsedOverrideResolution;
        proposal.Parameters.TryGetValue(InstanceWindowWidthParameterKey, out var rawWindowWidth);
        var finalWindowWidth = ParseStoredInt32(rawWindowWidth);
        proposal.Parameters.TryGetValue(InstanceWindowHeightParameterKey, out var rawWindowHeight);
        var finalWindowHeight = ParseStoredInt32(rawWindowHeight);
        proposal.Parameters.TryGetValue(InstanceGameDirectoryModeParameterKey, out var rawGameDirectoryMode);
        var finalGameDirectoryMode = NullIfWhiteSpace(rawGameDirectoryMode);
        proposal.Parameters.TryGetValue(InstanceCustomGameDirectoryPathParameterKey, out var rawCustomGameDirectoryPath);
        var finalCustomGameDirectoryPath = NullIfWhiteSpace(rawCustomGameDirectoryPath);
        var finalUseGlobalSettingsOverall = proposal.Parameters.TryGetValue(InstanceUseGlobalSettingsOverallParameterKey, out var rawUseGlobalSettingsOverall)
            && bool.TryParse(rawUseGlobalSettingsOverall, out var parsedUseGlobalSettingsOverall)
                ? parsedUseGlobalSettingsOverall
                : UsesGlobalSettingsOverall(finalUseGlobalJavaSetting, finalOverrideMemory, finalOverrideResolution);

        if (!finalUseGlobalJavaSetting && string.IsNullOrWhiteSpace(finalJavaPath) && finalUseGlobalSettingsOverall)
        {
            return "实例独立 Java 模式必须保留一个有效的 Java 路径。";
        }

        if (!finalUseGlobalJavaSetting && !string.IsNullOrWhiteSpace(finalJavaPath))
        {
            var ensureResult = await EnsureJavaPresentInKnownListAsync(finalJavaPath!, cancellationToken);
            if (!ensureResult.Success)
            {
                return ensureResult.ErrorMessage;
            }

            if (ensureResult.KnownJavaVersionsChanged)
            {
                await _gameSettingsDomainService.SaveJavaVersionsAsync(ensureResult.KnownJavaVersions);
            }
        }

        var currentConfig = await _versionInfoService.GetFullVersionInfoAsync(
            targetVersion.VersionName,
            targetVersion.VersionDirectoryPath,
            preferCache: true);
        cancellationToken.ThrowIfCancellationRequested();

        var settings = CreateVersionSettings(currentConfig);
        settings.UseGlobalJavaSetting = finalUseGlobalJavaSetting;
        settings.JavaPath = finalJavaPath ?? string.Empty;
        settings.CustomJvmArguments = finalCustomJvmArguments;
        settings.GarbageCollectorMode = finalGarbageCollectorMode;
        settings.OverrideMemory = finalOverrideMemory;
        settings.AutoMemoryAllocation = finalAutoMemoryAllocation;
        settings.InitialHeapMemory = finalInitialHeapMemory;
        settings.MaximumHeapMemory = finalMaximumHeapMemory;
        settings.OverrideResolution = finalOverrideResolution;
        settings.WindowWidth = finalWindowWidth;
        settings.WindowHeight = finalWindowHeight;
        settings.GameDirMode = finalGameDirectoryMode;
        settings.GameDirCustomPath = finalCustomGameDirectoryPath;

        await _versionSettingsOrchestrator.SaveVersionSettingsAsync(
            new VersionListViewModel.VersionInfoItem
            {
                Name = targetVersion.VersionName,
                Path = targetVersion.VersionDirectoryPath,
            },
            settings);

        return $"已更新实例 {targetVersion.VersionName} 的启动设置：{BuildChangeSummary(proposal)}。";
    }

    private async Task<ResolvedTargetVersion> ResolveTargetVersionAsync(
        string? requestedTargetVersionName,
        string? requestedTargetVersionPath,
        CancellationToken cancellationToken)
    {
        var currentMinecraftPath = await _gameSettingsDomainService.ResolveCurrentMinecraftPathAsync();
        var normalizedTargetVersionName = NormalizeText(requestedTargetVersionName);
        var normalizedTargetVersionPath = NormalizeDirectoryPath(requestedTargetVersionPath);
        if (string.IsNullOrWhiteSpace(normalizedTargetVersionName) && string.IsNullOrWhiteSpace(normalizedTargetVersionPath))
        {
            return ResolvedTargetVersion.CreateFailure("必须提供 target_version_name，或提供 target_version_path 让启动器推导目标实例。建议先调用 get_instances。");
        }

        if (string.IsNullOrWhiteSpace(normalizedTargetVersionName) && !string.IsNullOrWhiteSpace(normalizedTargetVersionPath))
        {
            normalizedTargetVersionName = Path.GetFileName(normalizedTargetVersionPath);
        }

        if (!string.IsNullOrWhiteSpace(normalizedTargetVersionName)
            && !string.IsNullOrWhiteSpace(normalizedTargetVersionPath)
            && !string.Equals(
                normalizedTargetVersionName,
                Path.GetFileName(normalizedTargetVersionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return ResolvedTargetVersion.CreateFailure("target_version_name 与 target_version_path 指向的实例不一致。请优先使用 get_instances 返回的 target_version_name / version_directory_path。");
        }

        var installedVersions = await _minecraftVersionService.GetInstalledVersionsAsync(currentMinecraftPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!installedVersions.Any(version => string.Equals(version, normalizedTargetVersionName, StringComparison.OrdinalIgnoreCase)))
        {
            return ResolvedTargetVersion.CreateFailure($"目标实例 {normalizedTargetVersionName} 不存在，请先调用 get_instances 获取当前目录下的可用实例。");
        }

        var resolvedTargetVersionPath = Path.Combine(currentMinecraftPath, MinecraftPathConsts.Versions, normalizedTargetVersionName!);
        if (!string.IsNullOrWhiteSpace(normalizedTargetVersionPath)
            && !PathEquals(normalizedTargetVersionPath, resolvedTargetVersionPath))
        {
            return ResolvedTargetVersion.CreateFailure("target_version_path 必须来自当前活动目录下 get_instances 返回的 version_directory_path。");
        }

        if (!Directory.Exists(resolvedTargetVersionPath))
        {
            return ResolvedTargetVersion.CreateFailure($"实例目录不存在：{resolvedTargetVersionPath}");
        }

        return ResolvedTargetVersion.CreateSuccess(normalizedTargetVersionName!, resolvedTargetVersionPath);
    }

    private async Task<List<JavaVersion>> LoadKnownJavaVersionsAsync(CancellationToken cancellationToken)
    {
        var knownJavaVersions = (await _gameSettingsDomainService.LoadJavaVersionsAsync())?.ToList() ?? [];
        if (knownJavaVersions.Count > 0)
        {
            return knownJavaVersions;
        }

        var detectedJavaVersions = await _javaRuntimeService.DetectJavaVersionsAsync(forceRefresh: false);
        cancellationToken.ThrowIfCancellationRequested();
        return detectedJavaVersions;
    }

    private async Task<JavaResolutionResult> ResolveJavaSelectionAsync(
        string? currentSelectedJavaPath,
        string? requestedJavaId,
        string? requestedJavaPath,
        IReadOnlyList<JavaVersion> knownJavaVersions,
        CancellationToken cancellationToken)
    {
        var workingKnownJavaVersions = knownJavaVersions.ToList();
        var normalizedRequestedJavaPath = NullIfWhiteSpace(requestedJavaPath);
        var knownJavaVersionsChanged = false;

        if (!string.IsNullOrWhiteSpace(normalizedRequestedJavaPath))
        {
            var currentInventory = AgentJavaInventoryHelper.NormalizeJavaVersions(currentSelectedJavaPath, workingKnownJavaVersions);
            if (!currentInventory.Any(entry => PathEquals(entry.Path, normalizedRequestedJavaPath)))
            {
                if (!Path.IsPathFullyQualified(normalizedRequestedJavaPath))
                {
                    return JavaResolutionResult.CreateFailure("selected_java_path / java_path 必须是绝对路径，或改用 checkJavaVersions 返回的 java_id。");
                }

                var javaVersion = await _javaRuntimeService.GetJavaVersionInfoAsync(normalizedRequestedJavaPath);
                cancellationToken.ThrowIfCancellationRequested();
                if (javaVersion == null)
                {
                    return JavaResolutionResult.CreateFailure($"无法解析 Java 路径：{normalizedRequestedJavaPath}。请确认它是有效的 java.exe / javaw.exe，或先调用 checkJavaVersions。");
                }

                workingKnownJavaVersions = MergeKnownJavaVersions(workingKnownJavaVersions, javaVersion);
                knownJavaVersionsChanged = true;
            }
        }

        var inventory = AgentJavaInventoryHelper.NormalizeJavaVersions(currentSelectedJavaPath, workingKnownJavaVersions);
        if (!AgentJavaInventoryHelper.TryResolveJava(
                requestedJavaId,
                normalizedRequestedJavaPath,
                inventory,
                out var javaEntry,
                out var errorMessage))
        {
            return JavaResolutionResult.CreateFailure(errorMessage);
        }

        return JavaResolutionResult.CreateSuccess(
            new ResolvedJavaSelection(javaEntry!.Path, BuildJavaDisplay(javaEntry)),
            workingKnownJavaVersions,
            knownJavaVersionsChanged);
    }

    private async Task<EnsureJavaKnownResult> EnsureJavaPresentInKnownListAsync(string javaPath, CancellationToken cancellationToken)
    {
        var knownJavaVersions = await LoadKnownJavaVersionsAsync(cancellationToken);
        if (knownJavaVersions.Any(javaVersion => PathEquals(javaVersion.Path, javaPath)))
        {
            return EnsureJavaKnownResult.CreateSuccess(knownJavaVersions, false);
        }

        var javaVersionInfo = await _javaRuntimeService.GetJavaVersionInfoAsync(javaPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (javaVersionInfo == null)
        {
            return EnsureJavaKnownResult.CreateFailure($"无法解析 Java 路径：{javaPath}。该路径可能已失效，请先重新调用 checkJavaVersions 或改用有效的 java_path。");
        }

        return EnsureJavaKnownResult.CreateSuccess(
            MergeKnownJavaVersions(knownJavaVersions, javaVersionInfo),
            true);
    }

    private static List<JavaVersion> MergeKnownJavaVersions(IEnumerable<JavaVersion> existingJavaVersions, JavaVersion javaVersion)
    {
        var mergedJavaVersions = existingJavaVersions.ToList();
        var existingIndex = mergedJavaVersions.FindIndex(existing => PathEquals(existing.Path, javaVersion.Path));
        if (existingIndex >= 0)
        {
            mergedJavaVersions[existingIndex] = javaVersion;
        }
        else
        {
            mergedJavaVersions.Add(javaVersion);
        }

        return mergedJavaVersions;
    }

    private static VersionSettings CreateVersionSettings(VersionConfig config)
    {
        return new VersionSettings
        {
            ModLoaderType = config.ModLoaderType,
            ModLoaderVersion = config.ModLoaderVersion,
            OptifineVersion = config.OptifineVersion,
            LiteLoaderVersion = config.LiteLoaderVersion,
            MinecraftVersion = config.MinecraftVersion,
            CreatedAt = config.CreatedAt,
            OverrideMemory = config.OverrideMemory,
            AutoMemoryAllocation = config.AutoMemoryAllocation,
            InitialHeapMemory = config.InitialHeapMemory,
            MaximumHeapMemory = config.MaximumHeapMemory,
            JavaPath = config.JavaPath,
            UseGlobalJavaSetting = config.UseGlobalJavaSetting,
            OverrideResolution = config.OverrideResolution,
            WindowWidth = config.WindowWidth,
            WindowHeight = config.WindowHeight,
            LaunchCount = config.LaunchCount,
            TotalPlayTimeSeconds = config.TotalPlayTimeSeconds,
            LastLaunchTime = config.LastLaunchTime,
            CustomJvmArguments = config.CustomJvmArguments ?? string.Empty,
            GarbageCollectorMode = config.GarbageCollectorMode,
            Icon = config.Icon,
            ModpackPlatform = config.ModpackPlatform,
            ModpackProjectId = config.ModpackProjectId,
            ModpackVersionId = config.ModpackVersionId,
            GameDirMode = config.GameDirMode,
            GameDirCustomPath = config.GameDirCustomPath,
        };
    }

    private static string DescribeGlobalJavaSelectionMode(string mode)
    {
        return string.Equals(mode, JavaSelectionModeManual, StringComparison.Ordinal)
            ? "手动"
            : "自动";
    }

    private static string DescribeInstanceJavaMode(bool useGlobalJavaSetting)
    {
        return useGlobalJavaSetting ? "跟随全局" : "版本独立";
    }

    private static string DescribeInstanceJavaValue(bool useGlobalJavaSetting, string? javaPath, IReadOnlyList<JavaVersion> knownJavaVersions)
    {
        return useGlobalJavaSetting
            ? "使用全局设置"
            : DescribeSelectedJava(javaPath, knownJavaVersions);
    }

    private static string DescribeSelectedJava(string? javaPath, IReadOnlyList<JavaVersion> knownJavaVersions)
    {
        var normalizedJavaPath = NullIfWhiteSpace(javaPath);
        if (string.IsNullOrWhiteSpace(normalizedJavaPath))
        {
            return "未设置";
        }

        var javaEntry = AgentJavaInventoryHelper.NormalizeJavaVersions(normalizedJavaPath, knownJavaVersions)
            .FirstOrDefault(entry => PathEquals(entry.Path, normalizedJavaPath));
        return javaEntry == null ? normalizedJavaPath : BuildJavaDisplay(javaEntry);
    }

    private static string DescribeJvmArguments(string? arguments)
    {
        return string.IsNullOrWhiteSpace(arguments) ? "未设置" : arguments;
    }

    private static string DescribeJvmGcSource(bool followsGlobal)
    {
        return followsGlobal ? "跟随全局" : "使用实例值";
    }

    private static string DescribeUseGlobalSettingsOverall(bool usesGlobalSettingsOverall)
    {
        return usesGlobalSettingsOverall ? "是" : "否";
    }

    private static string DescribeJvmGcSourceAfterPatch(bool followsGlobal, bool hasJvmSettingsRequest)
    {
        if (!followsGlobal)
        {
            return "使用实例值";
        }

        return hasJvmSettingsRequest
            ? "仍跟随全局（本地值已更新，待实例脱离全局后生效）"
            : "跟随全局";
    }

    private static string DescribeMemorySource(bool followsGlobal)
    {
        return followsGlobal ? "跟随全局" : "使用实例值";
    }

    private static string DescribeMemoryAllocationMode(bool autoMemoryAllocation)
    {
        return autoMemoryAllocation ? "自动管理" : "手动指定";
    }

    private static string DescribeMemoryAmount(double value)
    {
        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} GB";
    }

    private static string DescribeResolutionSource(bool followsGlobal)
    {
        return followsGlobal ? "跟随全局" : "使用实例值";
    }

    private static string DescribeGlobalGameDirectoryMode(string mode)
    {
        return mode switch
        {
            GameDirectoryModeDefault => "默认目录",
            GameDirectoryModeVersionIsolation => "版本隔离",
            GameDirectoryModeCustom => "自定义目录",
            _ => "默认目录",
        };
    }

    private static string DescribeInstanceGameDirectoryMode(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode)
            ? "跟随全局"
            : DescribeGlobalGameDirectoryMode(mode);
    }

    private static string DescribeGameDirectoryPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "未设置" : path;
    }

    private static string DescribeGameDirectorySource(bool followsGlobal)
    {
        return followsGlobal ? "跟随全局" : "使用实例值";
    }

    private static string DescribeGameDirectorySourceAfterPatch(bool followsGlobal, bool usesGlobalSettingsOverall, string? localMode, bool hasGameDirectorySettingsRequest)
    {
        if (!followsGlobal)
        {
            return "使用实例值";
        }

        if (hasGameDirectorySettingsRequest
            && usesGlobalSettingsOverall
            && !string.IsNullOrWhiteSpace(localMode))
        {
            return "仍跟随全局（本地目录模式已更新，待实例脱离全局后生效）";
        }

        return "跟随全局";
    }

    private static string DescribeWindowSize(int value)
    {
        return $"{value.ToString(CultureInfo.InvariantCulture)} px";
    }

    private static string BuildJavaDisplay(AgentJavaInventoryEntry javaEntry)
    {
        var javaType = javaEntry.IsJdk ? "JDK" : "JRE";
        return $"Java {javaEntry.MajorVersion} ({javaEntry.FullVersion}, {javaType}) - {javaEntry.Path}";
    }

    private static string NormalizeJavaSelectionMode(string? rawMode)
    {
        return string.Equals(rawMode, JavaSelectionModeManual, StringComparison.OrdinalIgnoreCase)
            ? JavaSelectionModeManual
            : JavaSelectionModeAuto;
    }

    private static string? NormalizeRequestedJavaSelectionMode(string? rawMode, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedMode = NormalizeText(rawMode);
        if (string.IsNullOrWhiteSpace(normalizedMode))
        {
            return null;
        }

        if (string.Equals(normalizedMode, JavaSelectionModeAuto, StringComparison.OrdinalIgnoreCase))
        {
            return JavaSelectionModeAuto;
        }

        if (string.Equals(normalizedMode, JavaSelectionModeManual, StringComparison.OrdinalIgnoreCase))
        {
            return JavaSelectionModeManual;
        }

        errorMessage = "java_selection_mode 仅支持 auto 或 manual。";
        return null;
    }

    private static string? NormalizeRequestedGarbageCollectorMode(string? rawMode, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(rawMode))
        {
            return null;
        }

        var normalized = GarbageCollectorModeHelper.Normalize(rawMode);
        if (!GarbageCollectorModeHelper.AllModes.Any(mode => string.Equals(mode, rawMode.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = $"garbage_collector_mode 仅支持 {string.Join(" / ", GarbageCollectorModeHelper.AllModes)}。";
            return null;
        }

        return normalized;
    }

    private static string? NormalizeRequestedGlobalGameDirectoryMode(string? rawMode, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedMode = NormalizeText(rawMode);
        if (string.IsNullOrWhiteSpace(normalizedMode))
        {
            return null;
        }

        return normalizedMode.ToLowerInvariant() switch
        {
            "default" => GameDirectoryModeDefault,
            "version_isolation" => GameDirectoryModeVersionIsolation,
            "versionisolation" => GameDirectoryModeVersionIsolation,
            "version-isolation" => GameDirectoryModeVersionIsolation,
            "custom" => GameDirectoryModeCustom,
            _ => SetGameDirectoryModeError("game_directory_mode 仅支持 default / version_isolation / custom。", out errorMessage),
        };
    }

    private static string? NormalizeRequestedInstanceGameDirectoryMode(string? rawMode, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedMode = NormalizeText(rawMode);
        if (string.IsNullOrWhiteSpace(normalizedMode))
        {
            return null;
        }

        return normalizedMode.ToLowerInvariant() switch
        {
            "follow_global" => InstanceGameDirectoryFollowGlobalToken,
            "followglobal" => InstanceGameDirectoryFollowGlobalToken,
            "global" => InstanceGameDirectoryFollowGlobalToken,
            "use_global" => InstanceGameDirectoryFollowGlobalToken,
            "default" => GameDirectoryModeDefault,
            "version_isolation" => GameDirectoryModeVersionIsolation,
            "versionisolation" => GameDirectoryModeVersionIsolation,
            "version-isolation" => GameDirectoryModeVersionIsolation,
            "custom" => GameDirectoryModeCustom,
            _ => SetGameDirectoryModeError("game_directory_mode 仅支持 follow_global / default / version_isolation / custom。", out errorMessage),
        };
    }

    private static string? SetGameDirectoryModeError(string error, out string errorMessage)
    {
        errorMessage = error;
        return null;
    }

    private static bool HasLocalGameDirectoryValueRequest(string? explicitGameDirectoryModeToken, bool hasCustomGameDirectoryPath)
    {
        return hasCustomGameDirectoryPath
            || (!string.IsNullOrWhiteSpace(explicitGameDirectoryModeToken)
                && !string.Equals(explicitGameDirectoryModeToken, InstanceGameDirectoryFollowGlobalToken, StringComparison.Ordinal));
    }

    private static bool? InferUseGlobalSettingsOverallFromLegacyInstanceFlags(
        bool? useGlobalJavaSetting,
        bool? overrideMemory,
        bool? overrideResolution,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var requestsGlobal = false;
        var requestsLocal = false;

        if (useGlobalJavaSetting.HasValue)
        {
            requestsGlobal |= useGlobalJavaSetting.Value;
            requestsLocal |= !useGlobalJavaSetting.Value;
        }

        if (overrideMemory.HasValue)
        {
            requestsGlobal |= !overrideMemory.Value;
            requestsLocal |= overrideMemory.Value;
        }

        if (overrideResolution.HasValue)
        {
            requestsGlobal |= !overrideResolution.Value;
            requestsLocal |= overrideResolution.Value;
        }

        if (!requestsGlobal && !requestsLocal)
        {
            return null;
        }

        if (requestsGlobal && requestsLocal)
        {
            errorMessage = "use_global_java_setting / override_memory / override_resolution 已废弃，且当前组合不符合 UI 的单一“使用全局设置”语义。请改用 use_global_settings_overall。";
            return null;
        }

        return requestsGlobal;
    }

    private static string ValidateMemoryValue(string fieldName, double value)
    {
        if (value < MinimumMemoryGb || value > MaximumMemoryGb)
        {
            return $"{fieldName} 必须位于 {MinimumMemoryGb.ToString("0.##", CultureInfo.InvariantCulture)} 到 {MaximumMemoryGb.ToString("0.##", CultureInfo.InvariantCulture)} GB 之间。";
        }

        return string.Empty;
    }

    private static string ValidateWindowWidth(int value)
    {
        if (value < MinimumWindowWidth || value > MaximumWindowWidth)
        {
            return $"window_width 必须位于 {MinimumWindowWidth} 到 {MaximumWindowWidth} 之间。";
        }

        return string.Empty;
    }

    private static string ValidateWindowHeight(int value)
    {
        if (value < MinimumWindowHeight || value > MaximumWindowHeight)
        {
            return $"window_height 必须位于 {MinimumWindowHeight} 到 {MaximumWindowHeight} 之间。";
        }

        return string.Empty;
    }

    private static string ToStoredJavaSelectionMode(string mode)
    {
        return string.Equals(mode, JavaSelectionModeManual, StringComparison.Ordinal)
            ? "Manual"
            : "Auto";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeJvmArguments(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static double ParseStoredDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : 0;
    }

    private static int ParseStoredInt32(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : 0;
    }

    private static bool ShouldUseLegacyVersionIsolation(string mode)
    {
        return !string.Equals(mode, GameDirectoryModeDefault, StringComparison.Ordinal);
    }

    private static string ResolveEffectiveGlobalGameDirectoryMode(string? rawMode, bool legacyEnableVersionIsolation)
    {
        if (string.Equals(rawMode, GameDirectoryModeDefault, StringComparison.OrdinalIgnoreCase))
        {
            return GameDirectoryModeDefault;
        }

        if (string.Equals(rawMode, GameDirectoryModeVersionIsolation, StringComparison.OrdinalIgnoreCase))
        {
            return GameDirectoryModeVersionIsolation;
        }

        if (string.Equals(rawMode, GameDirectoryModeCustom, StringComparison.OrdinalIgnoreCase))
        {
            return GameDirectoryModeCustom;
        }

        return legacyEnableVersionIsolation ? GameDirectoryModeVersionIsolation : GameDirectoryModeDefault;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeDirectoryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 3
            ? trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : trimmed;
    }

    private static bool PathEquals(string? left, string? right)
    {
        return string.Equals(NormalizeDirectoryPath(left), NormalizeDirectoryPath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoubleEquals(double left, double right)
    {
        return Math.Abs(left - right) < 0.0001;
    }

    private static bool JvmSettingsFollowGlobal(bool useGlobalJavaSetting, bool overrideMemory, bool overrideResolution)
    {
        return UsesGlobalSettingsOverall(useGlobalJavaSetting, overrideMemory, overrideResolution);
    }

    private static bool UsesGlobalSettingsOverall(bool useGlobalJavaSetting, bool overrideMemory, bool overrideResolution)
    {
        return useGlobalJavaSetting && !overrideMemory && !overrideResolution;
    }

    private static bool GameDirectoryFollowsGlobal(bool usesGlobalSettingsOverall, string? localMode)
    {
        return usesGlobalSettingsOverall || string.IsNullOrWhiteSpace(localMode);
    }

    private string BuildChangeSummary(AgentActionProposal proposal)
    {
        if (_proposalService.TryParsePayload(proposal, out var payload)
            && payload is { Changes.Count: > 0 })
        {
            return string.Join("、", payload.Changes
                .Select(change => change.DisplayName)
                .Distinct(StringComparer.Ordinal));
        }

        return "设置项";
    }

    private const string JavaSelectionModeAuto = "auto";
    private const string JavaSelectionModeManual = "manual";

    private sealed record ResolvedJavaSelection(string Path, string DisplayText);

    private sealed class JavaResolutionResult
    {
        private JavaResolutionResult(bool success, ResolvedJavaSelection? selection, IReadOnlyList<JavaVersion> knownJavaVersions, bool knownJavaVersionsChanged, string errorMessage)
        {
            Success = success;
            Selection = selection;
            KnownJavaVersions = knownJavaVersions;
            KnownJavaVersionsChanged = knownJavaVersionsChanged;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }

        public ResolvedJavaSelection? Selection { get; }

        public IReadOnlyList<JavaVersion> KnownJavaVersions { get; }

        public bool KnownJavaVersionsChanged { get; }

        public string ErrorMessage { get; }

        public static JavaResolutionResult CreateSuccess(ResolvedJavaSelection selection, IReadOnlyList<JavaVersion> knownJavaVersions, bool knownJavaVersionsChanged)
        {
            return new JavaResolutionResult(true, selection, knownJavaVersions, knownJavaVersionsChanged, string.Empty);
        }

        public static JavaResolutionResult CreateFailure(string errorMessage)
        {
            return new JavaResolutionResult(false, null, [], false, errorMessage);
        }
    }

    private sealed class EnsureJavaKnownResult
    {
        private EnsureJavaKnownResult(bool success, IReadOnlyList<JavaVersion> knownJavaVersions, bool knownJavaVersionsChanged, string errorMessage)
        {
            Success = success;
            KnownJavaVersions = knownJavaVersions;
            KnownJavaVersionsChanged = knownJavaVersionsChanged;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }

        public IReadOnlyList<JavaVersion> KnownJavaVersions { get; }

        public bool KnownJavaVersionsChanged { get; }

        public string ErrorMessage { get; }

        public static EnsureJavaKnownResult CreateSuccess(IReadOnlyList<JavaVersion> knownJavaVersions, bool knownJavaVersionsChanged)
        {
            return new EnsureJavaKnownResult(true, knownJavaVersions, knownJavaVersionsChanged, string.Empty);
        }

        public static EnsureJavaKnownResult CreateFailure(string errorMessage)
        {
            return new EnsureJavaKnownResult(false, [], false, errorMessage);
        }
    }

    private sealed class ResolvedTargetVersion
    {
        private ResolvedTargetVersion(bool success, string versionName, string versionDirectoryPath, string errorMessage)
        {
            Success = success;
            VersionName = versionName;
            VersionDirectoryPath = versionDirectoryPath;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }

        public string VersionName { get; }

        public string VersionDirectoryPath { get; }

        public string ErrorMessage { get; }

        public static ResolvedTargetVersion CreateSuccess(string versionName, string versionDirectoryPath)
        {
            return new ResolvedTargetVersion(true, versionName, versionDirectoryPath, string.Empty);
        }

        public static ResolvedTargetVersion CreateFailure(string errorMessage)
        {
            return new ResolvedTargetVersion(false, string.Empty, string.Empty, errorMessage);
        }
    }
}