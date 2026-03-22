using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class CrashReportDialogService : ICrashReportDialogService
{
    private readonly IContentDialogHostService _dialogHostService;
    private readonly IDialogThemePaletteService _dialogThemePaletteService;
    private readonly IUiDispatcher _uiDispatcher;

    public CrashReportDialogService(
        IContentDialogHostService dialogHostService,
        IDialogThemePaletteService dialogThemePaletteService,
        IUiDispatcher uiDispatcher)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
        _dialogThemePaletteService = dialogThemePaletteService ?? throw new ArgumentNullException(nameof(dialogThemePaletteService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
    }

    public async Task<CrashReportDialogAction> ShowCrashReportDialogAsync(
        string crashTitle,
        string crashAnalysis,
        string fullLog,
        bool isEasterEggMode)
    {
        var errorRedColor = Windows.UI.Color.FromArgb(255, 196, 43, 28);
        var errorBgColor = Windows.UI.Color.FromArgb(30, 232, 17, 35);

        var warningPanel = new StackPanel { Spacing = 20, Margin = new Thickness(0) };
        var warningCard = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorBgColor),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16),
        };

        var warningCardContent = new StackPanel { Spacing = 12 };
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var warningIcon = new FontIcon
        {
            Glyph = "\uE7BA",
            FontSize = 24,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor),
        };

        var titleText = string.IsNullOrWhiteSpace(crashTitle)
            ? "游戏意外退出"
            : $"游戏意外退出：{crashTitle}";
        var warningTitle = new TextBlock
        {
            Text = titleText,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorRedColor),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        headerStack.Children.Add(warningIcon);
        headerStack.Children.Add(warningTitle);
        warningCardContent.Children.Add(headerStack);

        var hintText = new TextBlock
        {
            Text = "为了快速解决问题，请导出完整的崩溃日志，而不是截图。",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _dialogThemePaletteService.GetPrimaryTextBrush(),
        };

        if (isEasterEggMode)
        {
            var scaleAnimation = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var scaleXAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut,
                },
            };

            hintText.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform();
            hintText.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");

            var scaleYAnimation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 1.0,
                To = 5.15,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.SineEase
                {
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut,
                },
            };

            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnimation, hintText);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");

            scaleAnimation.Children.Add(scaleXAnimation);
            scaleAnimation.Children.Add(scaleYAnimation);
            hintText.Loaded += (_, _) => scaleAnimation.Begin();
        }

        warningCardContent.Children.Add(hintText);
        warningCard.Child = warningCardContent;
        warningPanel.Children.Add(warningCard);

        var instructionCard = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16),
            Background = _dialogThemePaletteService.GetCardBackgroundBrush(),
            BorderBrush = _dialogThemePaletteService.GetCardStrokeBrush(),
            BorderThickness = new Thickness(1),
        };

        var instructionStack = new StackPanel { Spacing = 10 };
        instructionStack.Children.Add(new TextBlock
        {
            Text = "正确的求助步骤",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = _dialogThemePaletteService.GetPrimaryTextBrush(),
        });
        instructionStack.Children.Add(new TextBlock { Text = "1. 点击下方「导出崩溃日志」按钮", FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = _dialogThemePaletteService.GetSecondaryTextBrush() });
        instructionStack.Children.Add(new TextBlock { Text = "2. 将导出的 ZIP 文件发送给技术支持", FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = _dialogThemePaletteService.GetSecondaryTextBrush() });
        instructionStack.Children.Add(new TextBlock
        {
            Text = "日志文件包含启动器日志、游戏日志等信息，能帮助快速定位问题",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _dialogThemePaletteService.GetTertiaryTextBrush(),
            Margin = new Thickness(0, 4, 0, 0),
        });

        instructionCard.Child = instructionStack;
        warningPanel.Children.Add(instructionCard);

        var logExpander = new Expander
        {
            Header = "查看日志预览",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        var logPreviewText = new TextBlock
        {
            Text = fullLog,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _dialogThemePaletteService.GetTertiaryTextBrush(),
        };
        logExpander.Content = new ScrollViewer
        {
            Content = logPreviewText,
            MaxHeight = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 0),
        };
        warningPanel.Children.Add(logExpander);

        var dialog = new ContentDialog
        {
            Title = "游戏崩溃",
            Content = warningPanel,
            PrimaryButtonText = "导出崩溃日志",
            SecondaryButtonText = "查看详细日志",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
        };

        CancellationTokenSource? shakeTokenSource = null;
        if (isEasterEggMode)
        {
            shakeTokenSource = new CancellationTokenSource();
            var shakeToken = shakeTokenSource.Token;

            _ = Task.Run(async () =>
            {
                var random = new Random();
                var originalPosition = new Windows.Graphics.PointInt32();
                var gotOriginalPosition = false;

                while (!shakeToken.IsCancellationRequested)
                {
                    try
                    {
                        _uiDispatcher.TryEnqueue(() =>
                        {
                            try
                            {
                                var appWindow = App.MainWindow.AppWindow;
                                if (!gotOriginalPosition)
                                {
                                    originalPosition = appWindow.Position;
                                    gotOriginalPosition = true;
                                }

                                var offsetX = random.Next(-15, 6);
                                var offsetY = random.Next(-15, 6);
                                appWindow.Move(new Windows.Graphics.PointInt32(originalPosition.X + offsetX, originalPosition.Y + offsetY));
                            }
                            catch
                            {
                            }
                        });

                        await Task.Delay(50, shakeToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (gotOriginalPosition)
                {
                    _uiDispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            App.MainWindow.AppWindow.Move(originalPosition);
                        }
                        catch
                        {
                        }
                    });
                }
            }, shakeToken);
        }

        dialog.Closed += (_, _) => shakeTokenSource?.Cancel();

        var result = await _dialogHostService.ShowAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => CrashReportDialogAction.ExportLogs,
            ContentDialogResult.Secondary => CrashReportDialogAction.ViewDetails,
            _ => CrashReportDialogAction.Close,
        };
    }
}