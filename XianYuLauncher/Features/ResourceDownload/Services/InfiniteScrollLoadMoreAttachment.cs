using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using XianYuLauncher.Contracts.Services;

namespace XianYuLauncher.Features.ResourceDownload.Services;

public static class InfiniteScrollLoadMoreAttachment
{
    private sealed class AttachmentState
    {
        public required Func<bool> IsActive { get; init; }
        public required Func<bool> CanLoadMore { get; init; }
        public required Action ExecuteLoadMore { get; init; }
        public required IUiDispatcher UiDispatcher { get; init; }
        public bool CheckPending { get; set; }
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(AttachmentState),
            typeof(InfiniteScrollLoadMoreAttachment),
            new PropertyMetadata(null));

    public static void Attach(
        ScrollViewer scrollViewer,
        Func<bool> isActive,
        Func<bool> canLoadMore,
        Action executeLoadMore,
        IUiDispatcher? uiDispatcher = null)
    {
        Detach(scrollViewer);

        var dispatcher = uiDispatcher ?? App.GetService<IUiDispatcher>();
        var state = new AttachmentState
        {
            IsActive = isActive,
            CanLoadMore = canLoadMore,
            ExecuteLoadMore = executeLoadMore,
            UiDispatcher = dispatcher
        };

        scrollViewer.SetValue(StateProperty, state);
        scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        scrollViewer.LayoutUpdated += ScrollViewer_LayoutUpdated;
        scrollViewer.SizeChanged += ScrollViewer_SizeChanged;
    }

    public static void Detach(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(StateProperty) is not AttachmentState)
        {
            return;
        }

        scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
        scrollViewer.LayoutUpdated -= ScrollViewer_LayoutUpdated;
        scrollViewer.SizeChanged -= ScrollViewer_SizeChanged;
        scrollViewer.ClearValue(StateProperty);
    }

    public static void ScheduleCheck(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(StateProperty) is not AttachmentState state)
        {
            return;
        }

        if (state.CheckPending)
        {
            return;
        }

        state.CheckPending = true;
        state.UiDispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                if (!state.IsActive() || scrollViewer.ViewportHeight <= 0)
                {
                    return;
                }

                TryLoadMore(scrollViewer, state);
            }
            catch (COMException)
            {
            }
            finally
            {
                state.CheckPending = false;
            }
        });
    }

    private static void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            TryLoadMoreImmediate(scrollViewer);
        }
    }

    private static void ScrollViewer_LayoutUpdated(object sender, object e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            ScheduleCheck(scrollViewer);
        }
    }

    private static void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            ScheduleCheck(scrollViewer);
        }
    }

    private static void TryLoadMoreImmediate(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(StateProperty) is not AttachmentState state || !state.IsActive())
        {
            return;
        }

        TryLoadMore(scrollViewer, state);
    }

    private static void TryLoadMore(ScrollViewer scrollViewer, AttachmentState state)
    {
        if (scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        if (IsNearScrollableBottom(scrollViewer) && state.CanLoadMore())
        {
            state.ExecuteLoadMore();
        }
    }

    private static bool IsNearScrollableBottom(ScrollViewer scrollViewer) =>
        scrollViewer.ScrollableHeight <= 0
        || scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 100;
}
