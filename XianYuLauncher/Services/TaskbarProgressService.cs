using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using WinRT.Interop;

using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Services;

public sealed class TaskbarProgressService : ITaskbarProgressService, IDisposable
{
    private static readonly Guid ClsidTaskbarList = new("56FDF344-FD6D-11D0-958A-006097C9A090");
    private static readonly Guid IidTaskbarList3 = new("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF");

    private const ulong TaskbarProgressValueScale = 1000;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EClassNotRegistered = unchecked((int)0x80040154);
    private const uint WmNcDestroy = 0x0082;
    private const nuint TaskbarWindowSubclassId = 1;
    private const uint ClsCtxInprocServer = 0x1;

    private readonly IUiDispatcher _uiDispatcher;
    private readonly ILogger<TaskbarProgressService> _logger;
    private readonly SubclassProc _windowSubclassProc;

    private ITaskbarList3? _taskbarList;
    private bool _isInitializationPermanentlyDisabled;
    private bool _hasLoggedInitializationFailure;
    private bool _isTaskbarButtonCreated;
    private bool _isWindowSubclassInstalled;
    private bool _hasLoggedWindowSubclassFailure;
    private nint _subclassedWindowHandle;
    private uint _taskbarButtonCreatedMessage;
    private bool _isProgressRequested;
    private double _requestedProgress;

    public TaskbarProgressService(IUiDispatcher uiDispatcher, ILogger<TaskbarProgressService> logger)
    {
        _uiDispatcher = uiDispatcher;
        _logger = logger;
        _windowSubclassProc = WindowSubclassCallback;

        ExecuteOnUiThread(() =>
        {
            _ = TryEnsureWindowSubclassInstalled();
        });
    }

    public void ShowProgress(double progress)
    {
        ExecuteOnUiThread(() =>
        {
            _isProgressRequested = true;
            _requestedProgress = Math.Clamp(progress, 0, 100);

            if (!TryEnsureWindowSubclassInstalled() || !_isTaskbarButtonCreated)
            {
                return;
            }

            ApplyProgressCore(_requestedProgress);
        });
    }

    public void ClearProgress()
    {
        ExecuteOnUiThread(() =>
        {
            _isProgressRequested = false;

            if (!TryEnsureWindowSubclassInstalled() || !_isTaskbarButtonCreated)
            {
                return;
            }

            ClearProgressCore();
        });
    }

    public void Dispose()
    {
        ExecuteOnUiThread(RemoveWindowSubclassCore);
        ResetTaskbarList();
    }

    private void ExecuteOnUiThread(Action action)
    {
        if (_uiDispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        if (!_uiDispatcher.TryEnqueue(action))
        {
            _logger.LogDebug("UI 调度器不可用，跳过任务栏进度更新。");
        }
    }

    private void ApplyProgressCore(double progress)
    {
        if (!TryEnsureTaskbarList(out var taskbarList) || !TryGetMainWindowHandle(out var windowHandle))
        {
            return;
        }

        ulong completedValue = (ulong)Math.Round(
            progress / 100d * TaskbarProgressValueScale,
            MidpointRounding.AwayFromZero);

        try
        {
            ThrowIfFailed(taskbarList.SetProgressState(windowHandle, TaskbarProgressState.Normal));
            ThrowIfFailed(taskbarList.SetProgressValue(windowHandle, completedValue, TaskbarProgressValueScale));
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            ResetTaskbarList();
            _logger.LogWarning(ex, "更新任务栏下载进度失败。Progress={Progress}", progress);
        }
    }

    private void ClearProgressCore()
    {
        if (!TryEnsureTaskbarList(out var taskbarList) || !TryGetMainWindowHandle(out var windowHandle))
        {
            return;
        }

        try
        {
            ThrowIfFailed(taskbarList.SetProgressState(windowHandle, TaskbarProgressState.NoProgress));
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            ResetTaskbarList();
            _logger.LogWarning(ex, "清空任务栏下载进度失败。");
        }
    }

    private bool TryEnsureWindowSubclassInstalled()
    {
        if (_isWindowSubclassInstalled)
        {
            return true;
        }

        if (!TryGetMainWindowHandle(out var windowHandle))
        {
            return false;
        }

        if (_taskbarButtonCreatedMessage == 0)
        {
            _taskbarButtonCreatedMessage = RegisterWindowMessage("TaskbarButtonCreated");
        }

        if (_taskbarButtonCreatedMessage == 0)
        {
            if (!_hasLoggedWindowSubclassFailure)
            {
                _hasLoggedWindowSubclassFailure = true;
                _logger.LogWarning("注册 TaskbarButtonCreated 窗口消息失败，任务栏进度将不可用。");
            }

            return false;
        }

        if (!SetWindowSubclass(windowHandle, _windowSubclassProc, TaskbarWindowSubclassId, nint.Zero))
        {
            if (!_hasLoggedWindowSubclassFailure)
            {
                _hasLoggedWindowSubclassFailure = true;
                _logger.LogWarning("安装任务栏窗口子类失败，任务栏进度将不可用。Hwnd={WindowHandle}", windowHandle);
            }

            return false;
        }

        _subclassedWindowHandle = windowHandle;
        _isWindowSubclassInstalled = true;
        return true;
    }

    private void RemoveWindowSubclassCore()
    {
        if (!_isWindowSubclassInstalled || _subclassedWindowHandle == nint.Zero)
        {
            return;
        }

        _ = RemoveWindowSubclass(_subclassedWindowHandle, _windowSubclassProc, TaskbarWindowSubclassId);
        _isWindowSubclassInstalled = false;
        _isTaskbarButtonCreated = false;
        _subclassedWindowHandle = nint.Zero;
    }

    private nint WindowSubclassCallback(nint hWnd, uint message, nint wParam, nint lParam, nuint subclassId, nint referenceData)
    {
        if (message == _taskbarButtonCreatedMessage)
        {
            _isTaskbarButtonCreated = true;
            if (_isProgressRequested)
            {
                ApplyProgressCore(_requestedProgress);
            }
            else
            {
                ClearProgressCore();
            }
        }
        else if (message == WmNcDestroy)
        {
            _ = RemoveWindowSubclass(hWnd, _windowSubclassProc, subclassId);
            _isWindowSubclassInstalled = false;
            _isTaskbarButtonCreated = false;
            _subclassedWindowHandle = nint.Zero;
        }

        return DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private bool TryEnsureTaskbarList(out ITaskbarList3 taskbarList)
    {
        if (_isInitializationPermanentlyDisabled)
        {
            taskbarList = null!;
            return false;
        }

        if (_taskbarList != null)
        {
            taskbarList = _taskbarList;
            return true;
        }

        try
        {
            var clsidTaskbarList = ClsidTaskbarList;
            var iidTaskbarList3 = IidTaskbarList3;
            int hr = CoCreateInstance(ref clsidTaskbarList, nint.Zero, ClsCtxInprocServer, ref iidTaskbarList3, out taskbarList);
            ThrowIfFailed(hr);
            ThrowIfFailed(taskbarList.HrInit());
            _taskbarList = taskbarList;
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            taskbarList = null!;
            ResetTaskbarList();

            if (ShouldPermanentlyDisableInitialization(ex))
            {
                _isInitializationPermanentlyDisabled = true;
            }

            if (!_hasLoggedInitializationFailure)
            {
                _hasLoggedInitializationFailure = true;
                _logger.LogWarning(ex, _isInitializationPermanentlyDisabled
                    ? "初始化任务栏进度服务失败，当前会话将禁用任务栏进度更新。"
                    : "初始化任务栏进度服务失败，后续将自动重试。");
            }

            return false;
        }
    }

    private static bool ShouldPermanentlyDisableInitialization(Exception exception)
    {
        return exception switch
        {
            InvalidCastException => true,
            COMException comException when comException.HResult == ENoInterface || comException.HResult == EClassNotRegistered => true,
            _ => false
        };
    }

    private static bool TryGetMainWindowHandle(out nint windowHandle)
    {
        try
        {
            windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
            return windowHandle != nint.Zero;
        }
        catch
        {
            windowHandle = nint.Zero;
            return false;
        }
    }

    private void ResetTaskbarList()
    {
        if (_taskbarList == null)
        {
            return;
        }

        if (Marshal.IsComObject(_taskbarList))
        {
            Marshal.FinalReleaseComObject(_taskbarList);
        }

        _taskbarList = null;
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private enum TaskbarProgressState : uint
    {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8
    }

    [ComImport]
    [Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        [PreserveSig]
        int HrInit();

        [PreserveSig]
        int AddTab(nint hwnd);

        [PreserveSig]
        int DeleteTab(nint hwnd);

        [PreserveSig]
        int ActivateTab(nint hwnd);

        [PreserveSig]
        int SetActiveAlt(nint hwnd);
    }

    [ComImport]
    [Guid("602D4995-B13A-429B-A66E-1935E44F4317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList2 : ITaskbarList
    {
        [PreserveSig]
        new int HrInit();

        [PreserveSig]
        new int AddTab(nint hwnd);

        [PreserveSig]
        new int DeleteTab(nint hwnd);

        [PreserveSig]
        new int ActivateTab(nint hwnd);

        [PreserveSig]
        new int SetActiveAlt(nint hwnd);

        [PreserveSig]
        int MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
    }

    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3 : ITaskbarList2
    {
        [PreserveSig]
        new int HrInit();

        [PreserveSig]
        new int AddTab(nint hwnd);

        [PreserveSig]
        new int DeleteTab(nint hwnd);

        [PreserveSig]
        new int ActivateTab(nint hwnd);

        [PreserveSig]
        new int SetActiveAlt(nint hwnd);

        [PreserveSig]
        new int MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);

        [PreserveSig]
        int SetProgressValue(nint hwnd, ulong completed, ulong total);

        [PreserveSig]
        int SetProgressState(nint hwnd, TaskbarProgressState flags);

        [PreserveSig]
        int RegisterTab(nint hwndTab, nint hwndMDI);

        [PreserveSig]
        int UnregisterTab(nint hwndTab);

        [PreserveSig]
        int SetTabOrder(nint hwndTab, nint hwndInsertBefore);

        [PreserveSig]
        int SetTabActive(nint hwndTab, nint hwndMDI, uint reserved);

        [PreserveSig]
        int ThumbBarAddButtons(nint hwnd, uint buttonCount, nint buttons);

        [PreserveSig]
        int ThumbBarUpdateButtons(nint hwnd, uint buttonCount, nint buttons);

        [PreserveSig]
        int ThumbBarSetImageList(nint hwnd, nint imageListHandle);

        [PreserveSig]
        int SetOverlayIcon(nint hwnd, nint iconHandle, [MarshalAs(UnmanagedType.LPWStr)] string? description);

        [PreserveSig]
        int SetThumbnailTooltip(nint hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? tip);

        [PreserveSig]
        int SetThumbnailClip(nint hwnd, nint clip);
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        [In] ref Guid rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out ITaskbarList3 ppv);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);

    [DllImport("comctl32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc subclassProc, nuint subclassId, nint referenceData);

    [DllImport("comctl32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc subclassProc, nuint subclassId);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(nint hWnd, uint message, nint wParam, nint lParam);

    private delegate nint SubclassProc(nint hWnd, uint message, nint wParam, nint lParam, nuint subclassId, nint referenceData);
}