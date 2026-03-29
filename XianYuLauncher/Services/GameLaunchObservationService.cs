using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.ErrorAnalysis.Services;

namespace XianYuLauncher.Services;

public enum GameLaunchObservationOrigin
{
    Unknown,
    LaunchPage,
    LauncherAi,
}

public sealed class GameLaunchObservedProcessExitedEventArgs : EventArgs
{
    public required GameLaunchObservationOrigin Origin { get; init; }

    public required string VersionId { get; init; }

    public required string MinecraftPath { get; init; }

    public required ProcessExitedEventArgs ProcessExitedEventArgs { get; init; }
}

public sealed class GameLaunchObservationRequest
{
    public required Process GameProcess { get; init; }

    public required string LaunchCommand { get; init; }

    public required string VersionId { get; init; }

    public required string MinecraftPath { get; init; }

    public GameLaunchObservationOrigin Origin { get; init; } = GameLaunchObservationOrigin.Unknown;

    public bool EnableLiveErrorAnalysisStreaming { get; init; }

    public Func<GameLaunchObservedProcessExitedEventArgs, Task>? ProcessExitedHandler { get; init; }
}

public interface IGameLaunchObservationService
{
    void Observe(GameLaunchObservationRequest request);

    void TerminateProcess(Process process, bool isUserTerminated = true);
}

public sealed class GameLaunchObservationService : IGameLaunchObservationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorAnalysisSessionCoordinator _errorAnalysisSessionCoordinator;
    private readonly IGameCrashWorkflowService _gameCrashWorkflowService;
    private readonly ILogger<GameLaunchObservationService> _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<int, ActiveObservation> _activeObservations = [];

    private sealed class ActiveObservation
    {
        public required Process GameProcess { get; init; }

        public required IGameProcessMonitor Monitor { get; init; }

        public required GameLaunchObservationRequest Request { get; init; }
    }

    public GameLaunchObservationService(
        IServiceProvider serviceProvider,
        IErrorAnalysisSessionCoordinator errorAnalysisSessionCoordinator,
        IGameCrashWorkflowService gameCrashWorkflowService,
        ILogger<GameLaunchObservationService> logger)
    {
        _serviceProvider = serviceProvider;
        _errorAnalysisSessionCoordinator = errorAnalysisSessionCoordinator;
        _gameCrashWorkflowService = gameCrashWorkflowService;
        _logger = logger;
    }

    public void Observe(GameLaunchObservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnableLiveErrorAnalysisStreaming)
        {
            _errorAnalysisSessionCoordinator.ClearLogsOnly();
            _errorAnalysisSessionCoordinator.SetLaunchCommand(request.LaunchCommand);
            _errorAnalysisSessionCoordinator.SetVersionInfo(request.VersionId, request.MinecraftPath);
        }

        var monitor = _serviceProvider.GetRequiredService<IGameProcessMonitor>();
        var processId = request.GameProcess.Id;
        var observation = new ActiveObservation
        {
            GameProcess = request.GameProcess,
            Monitor = monitor,
            Request = request,
        };

        monitor.OutputReceived += (_, e) =>
        {
            if (request.EnableLiveErrorAnalysisStreaming)
            {
                _errorAnalysisSessionCoordinator.AddGameOutputLog(e.Line);
            }
        };

        monitor.ErrorReceived += (_, e) =>
        {
            if (request.EnableLiveErrorAnalysisStreaming)
            {
                _errorAnalysisSessionCoordinator.AddGameErrorLog(e.Line);
            }
        };

        monitor.ProcessExited += async (_, e) =>
        {
            RemoveObservation(processId);

            var observedArgs = new GameLaunchObservedProcessExitedEventArgs
            {
                Origin = request.Origin,
                VersionId = request.VersionId,
                MinecraftPath = request.MinecraftPath,
                ProcessExitedEventArgs = e,
            };

            if (request.ProcessExitedHandler != null)
            {
                try
                {
                    await request.ProcessExitedHandler(observedArgs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "处理游戏退出回调失败: {VersionId}", request.VersionId);
                }
            }

            if (e.ExitCode != 0 && !e.IsUserTerminated)
            {
                try
                {
                    await _gameCrashWorkflowService.HandleCrashAsync(new GameCrashContext
                    {
                        ExitCode = e.ExitCode,
                        LaunchCommand = e.LaunchCommand,
                        GameOutput = e.OutputLogs,
                        GameError = e.ErrorLogs,
                        VersionId = request.VersionId,
                        MinecraftPath = request.MinecraftPath,
                        Origin = request.Origin,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "处理游戏崩溃工作流失败: {VersionId}", request.VersionId);
                }
            }
        };

        lock (_lock)
        {
            _activeObservations[processId] = observation;
        }

        _ = monitor.MonitorProcessAsync(request.GameProcess, request.LaunchCommand).ContinueWith(
            task =>
            {
                if (task.Exception != null)
                {
                    RemoveObservation(processId);
                    _logger.LogWarning(task.Exception.GetBaseException(), "游戏进程监控失败: {VersionId}", request.VersionId);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    public void TerminateProcess(Process process, bool isUserTerminated = true)
    {
        ArgumentNullException.ThrowIfNull(process);

        ActiveObservation? observation;
        lock (_lock)
        {
            _activeObservations.TryGetValue(process.Id, out observation);
        }

        if (observation != null)
        {
            observation.Monitor.TerminateProcess(process, isUserTerminated);
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "终止未受管进程失败: {ProcessId}", process.Id);
        }
    }

    private void RemoveObservation(int processId)
    {
        lock (_lock)
        {
            _activeObservations.Remove(processId);
        }
    }
}