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
    private const string InstanceUseGlobalJavaSettingParameterKey = "use_global_java_setting";
    private const string InstanceJavaPathParameterKey = "java_path";
    private const string InstanceCustomJvmArgumentsParameterKey = "custom_jvm_arguments";
    private const string InstanceGarbageCollectorModeParameterKey = "garbage_collector_mode";
    private const string InstanceOverrideMemoryParameterKey = "override_memory";
    private const string InstanceAutoMemoryAllocationParameterKey = "auto_memory_allocation";
    private const string InstanceInitialHeapMemoryParameterKey = "initial_heap_memory_gb";
    private const string InstanceMaximumHeapMemoryParameterKey = "maximum_heap_memory_gb";

    private readonly IGameSettingsDomainService _gameSettingsDomainService;
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IMinecraftVersionService _minecraftVersionService;
    private readonly IVersionInfoService _versionInfoService;
    private readonly IVersionSettingsOrchestrator _versionSettingsOrchestrator;
    private readonly IAgentSettingsActionProposalService _proposalService;

    private const double MinimumMemoryGb = 1;
    private const double MaximumMemoryGb = 64;

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

        var clearSelectedJava = request.ClearSelectedJava;
        if (clearSelectedJava
            && (!string.IsNullOrWhiteSpace(request.SelectedJavaId) || !string.IsNullOrWhiteSpace(request.SelectedJavaPath)))
        {
            return AgentToolExecutionResult.FromMessage("clear_selected_java 与 selected_java_id/selected_java_path 不能同时使用。");
        }

        var hasSelectedJavaRequest = !string.IsNullOrWhiteSpace(request.SelectedJavaId)
            || !string.IsNullOrWhiteSpace(request.SelectedJavaPath);
        var hasJvmSettingsRequest = request.HasCustomJvmArguments || explicitGarbageCollectorMode != null;
        if (explicitMode == null && !clearSelectedJava && !hasSelectedJavaRequest && !hasJvmSettingsRequest)
        {
            return AgentToolExecutionResult.FromMessage("至少需要提供一个变更字段，例如 java_selection_mode、selected_java_id、selected_java_path、clear_selected_java、custom_jvm_arguments 或 garbage_collector_mode。调用前建议先使用 getGlobalLaunchSettings 和 checkJavaVersions。");
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

        if (changes.Count == 0)
        {
            return AgentToolExecutionResult.FromMessage("全局 Java/JVM/GC/内存设置未发生变化。");
        }

        var proposal = _proposalService.CreateProposal(
            PatchGlobalLaunchSettingsToolHandler.ToolNameValue,
            "应用全局 Java 设置",
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
        var hasMemorySettingsRequest = request.OverrideMemory != null
            || request.AutoMemoryAllocation != null
            || request.InitialHeapMemoryGb.HasValue
            || request.MaximumHeapMemoryGb.HasValue;
        if (request.UseGlobalJavaSetting == null && !hasJavaRequest && !hasJvmSettingsRequest && !hasMemorySettingsRequest)
        {
            return AgentToolExecutionResult.FromMessage("至少需要提供一个实例级变更字段，例如 use_global_java_setting、java_id、java_path、custom_jvm_arguments、garbage_collector_mode、override_memory、auto_memory_allocation、initial_heap_memory_gb 或 maximum_heap_memory_gb。调用前建议先使用 get_instances、getVersionConfig 和 checkJavaVersions。");
        }

        if (request.UseGlobalJavaSetting == true && hasJavaRequest)
        {
            return AgentToolExecutionResult.FromMessage("当 use_global_java_setting=true 时，不能同时指定 java_id 或 java_path。若要设置实例独立 Java，请将 use_global_java_setting 设为 false，或直接省略该字段让工具自动切到实例独立模式。");
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

        var finalUseGlobalJavaSetting = request.UseGlobalJavaSetting ?? currentUseGlobalJavaSetting;
        var finalJavaPath = currentJavaPath;
        var finalCustomJvmArguments = request.HasCustomJvmArguments
            ? NormalizeJvmArguments(request.CustomJvmArguments)
            : currentCustomJvmArguments;
        var finalGarbageCollectorMode = explicitGarbageCollectorMode ?? currentGarbageCollectorMode;
        var finalOverrideMemory = request.OverrideMemory ?? currentOverrideMemory;
        var finalAutoMemoryAllocation = request.AutoMemoryAllocation ?? currentAutoMemoryAllocation;
        var finalInitialHeapMemory = request.InitialHeapMemoryGb ?? currentInitialHeapMemory;
        var finalMaximumHeapMemory = request.MaximumHeapMemoryGb ?? currentMaximumHeapMemory;
        if (requestedJava != null)
        {
            finalUseGlobalJavaSetting = false;
            finalJavaPath = requestedJava.Path;
        }

        if (!finalUseGlobalJavaSetting && string.IsNullOrWhiteSpace(finalJavaPath))
        {
            return AgentToolExecutionResult.FromMessage("实例独立 Java 模式必须提供一个可用的 java_id 或 java_path；若要让实例继续跟随全局，请将 use_global_java_setting 设为 true。");
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
            return AgentToolExecutionResult.FromMessage("当 override_memory=false 时，不能同时设置 auto_memory_allocation、initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改实例独立内存，请先将 override_memory 设为 true。");
        }

        if (finalOverrideMemory && finalAutoMemoryAllocation && (request.InitialHeapMemoryGb.HasValue || request.MaximumHeapMemoryGb.HasValue))
        {
            return AgentToolExecutionResult.FromMessage("当 auto_memory_allocation=true 时，不能同时设置 initial_heap_memory_gb 或 maximum_heap_memory_gb。若要修改手动内存，请先将 auto_memory_allocation 设为 false。");
        }

        if (finalOverrideMemory && !finalAutoMemoryAllocation && finalInitialHeapMemory > finalMaximumHeapMemory)
        {
            return AgentToolExecutionResult.FromMessage("initial_heap_memory_gb 不能大于 maximum_heap_memory_gb。");
        }

        var currentJvmSettingsFollowGlobal = JvmSettingsFollowGlobal(currentUseGlobalJavaSetting, currentConfig.OverrideMemory, currentConfig.OverrideResolution);
        var finalJvmSettingsFollowGlobal = JvmSettingsFollowGlobal(finalUseGlobalJavaSetting, finalOverrideMemory, currentConfig.OverrideResolution);

        List<AgentSettingsFieldChange> changes = [];
        if (currentUseGlobalJavaSetting != finalUseGlobalJavaSetting)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "use_global_java_setting",
                DisplayName = "Java 设置模式",
                OldValue = DescribeInstanceJavaMode(currentUseGlobalJavaSetting),
                NewValue = DescribeInstanceJavaMode(finalUseGlobalJavaSetting),
                SwitchesToFollowGlobal = finalUseGlobalJavaSetting,
                SwitchesToOverride = !finalUseGlobalJavaSetting,
            });
        }

        var oldJavaDisplay = DescribeInstanceJavaValue(currentUseGlobalJavaSetting, currentJavaPath, knownJavaVersions);
        var newJavaDisplay = DescribeInstanceJavaValue(finalUseGlobalJavaSetting, finalJavaPath, knownJavaVersions);
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

        if (hasJvmSettingsRequest && (currentJvmSettingsFollowGlobal != finalJvmSettingsFollowGlobal || finalJvmSettingsFollowGlobal))
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "jvm_gc_effective_source",
                DisplayName = "JVM/GC 生效方式",
                OldValue = DescribeJvmGcSource(currentJvmSettingsFollowGlobal),
                NewValue = DescribeJvmGcSourceAfterPatch(finalJvmSettingsFollowGlobal, hasJvmSettingsRequest),
                SwitchesToFollowGlobal = !currentJvmSettingsFollowGlobal && finalJvmSettingsFollowGlobal,
                SwitchesToOverride = currentJvmSettingsFollowGlobal && !finalJvmSettingsFollowGlobal,
            });
        }

        if (currentOverrideMemory != finalOverrideMemory)
        {
            changes.Add(new AgentSettingsFieldChange
            {
                FieldKey = "override_memory",
                DisplayName = "内存来源",
                OldValue = DescribeMemorySource(!currentOverrideMemory),
                NewValue = DescribeMemorySource(!finalOverrideMemory),
                SwitchesToFollowGlobal = currentOverrideMemory && !finalOverrideMemory,
                SwitchesToOverride = !currentOverrideMemory && finalOverrideMemory,
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

        if (changes.Count == 0)
        {
            return AgentToolExecutionResult.FromMessage($"实例 {targetVersion.VersionName} 的 Java/JVM/GC/内存设置未发生变化。");
        }

        var proposal = _proposalService.CreateProposal(
            PatchInstanceLaunchSettingsToolHandler.ToolNameValue,
            $"更新 {targetVersion.VersionName} 的 Java 设置",
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
        if (!finalUseGlobalJavaSetting && string.IsNullOrWhiteSpace(finalJavaPath))
        {
            return "实例独立 Java 模式必须保留一个有效的 Java 路径。";
        }

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

        if (!finalUseGlobalJavaSetting)
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

    private static string ValidateMemoryValue(string fieldName, double value)
    {
        if (value < MinimumMemoryGb || value > MaximumMemoryGb)
        {
            return $"{fieldName} 必须位于 {MinimumMemoryGb.ToString("0.##", CultureInfo.InvariantCulture)} 到 {MaximumMemoryGb.ToString("0.##", CultureInfo.InvariantCulture)} GB 之间。";
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
        return useGlobalJavaSetting && !overrideMemory && !overrideResolution;
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