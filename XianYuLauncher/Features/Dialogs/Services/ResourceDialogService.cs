using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class ResourceDialogService : IResourceDialogService
{
    private readonly ICommonDialogService _commonDialogService;
    private readonly IContentDialogHostService _dialogHostService;
    private readonly IDialogThemePaletteService _dialogThemePaletteService;

    public ResourceDialogService(
        ICommonDialogService commonDialogService,
        IContentDialogHostService dialogHostService,
        IDialogThemePaletteService dialogThemePaletteService)
    {
        _commonDialogService = commonDialogService ?? throw new ArgumentNullException(nameof(commonDialogService));
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
        _dialogThemePaletteService = dialogThemePaletteService ?? throw new ArgumentNullException(nameof(dialogThemePaletteService));
    }

    public async Task ShowFavoritesImportResultDialogAsync(IEnumerable<XianYuLauncher.Models.FavoritesImportResultItem> results)
    {
        var resultList = results.ToList();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "以下资源不支持所选版本：", FontSize = 14 });

        var listView = new ListView
        {
            MaxHeight = 400,
            ItemsSource = resultList,
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
        };
        listView.ContainerContentChanging += (_, args) =>
        {
            if (args.Phase != 0)
            {
                return;
            }

            args.Handled = true;
            if (args.Item is not XianYuLauncher.Models.FavoritesImportResultItem item)
            {
                return;
            }

            var opacity = item.IsGrayedOut ? 0.5 : 1.0;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = item.ItemName,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = opacity,
            });
            row.Children.Add(new TextBlock
            {
                Text = item.StatusText,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = opacity,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            });
            args.ItemContainer.Content = row;
        };

        panel.Children.Add(listView);
        await _commonDialogService.ShowCustomDialogAsync("部分资源不支持此版本", panel, primaryButtonText: "确定", closeButtonText: null);
    }

    public async Task<string?> ShowModpackInstallNameDialogAsync(
        string defaultName,
        string? tip = null,
        Func<string, (bool IsValid, string ErrorMessage)>? validateInput = null)
    {
        var primaryTextBrush = _dialogThemePaletteService.GetPrimaryTextBrush();
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();
        var criticalTextBrush = _dialogThemePaletteService.GetCriticalTextBrush();

        var inputBox = new TextBox
        {
            Text = defaultName?.Trim() ?? string.Empty,
            PlaceholderText = "ModDownloadDetailPage_ModpackInstallNameDialog_PlaceholderText".GetLocalized(),
            MinWidth = 380,
            Width = 380,
        };

        var instructionText = new TextBlock
        {
            Text = "ModDownloadDetailPage_ModpackInstallNameDialog_InstructionText".GetLocalized(),
            FontSize = 14,
            Foreground = primaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
        };

        var errorText = new TextBlock
        {
            FontSize = 12,
            Foreground = criticalTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(instructionText);
        content.Children.Add(inputBox);
        content.Children.Add(errorText);

        if (!string.IsNullOrWhiteSpace(tip))
        {
            content.Children.Add(new TextBlock
            {
                Text = tip,
                FontSize = 12,
                Foreground = secondaryTextBrush,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var dialog = new ContentDialog
        {
            Title = "ModDownloadDetailPage_ModpackInstallNameDialog_Title".GetLocalized(),
            Content = content,
            PrimaryButtonText = "ModDownloadDetailPage_ModpackInstallNameDialog_PrimaryButtonText".GetLocalized(),
            CloseButtonText = "ModDownloadDetailPage_ModpackInstallNameDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };

        void UpdateValidationState()
        {
            var value = inputBox.Text ?? string.Empty;
            var validationResult = validateInput?.Invoke(value)
                ?? (!string.IsNullOrWhiteSpace(value), !string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : "ModDownloadDetailPage_ModpackInstallNameDialog_Error_Empty".GetLocalized());

            dialog.IsPrimaryButtonEnabled = validationResult.IsValid;
            errorText.Text = validationResult.IsValid ? string.Empty : validationResult.ErrorMessage;
            errorText.Visibility = string.IsNullOrWhiteSpace(errorText.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        inputBox.TextChanged += (_, _) => UpdateValidationState();
        dialog.Opened += (_, _) =>
        {
            inputBox.Focus(FocusState.Programmatic);
            inputBox.SelectionStart = inputBox.Text.Length;
            UpdateValidationState();
        };

        UpdateValidationState();

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return inputBox.Text?.Trim();
    }

    public async Task<ContentDialogResult> ShowDownloadMethodDialogAsync(
        string title,
        string instruction,
        IEnumerable<object>? dependencyProjects,
        bool isLoadingDependencies,
        Action<string>? onDependencyClick)
    {
        var primaryTextBrush = _dialogThemePaletteService.GetPrimaryTextBrush();
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();
        var cardBgBrush = _dialogThemePaletteService.GetCardBackgroundBrush();
        var cardStrokeBrush = _dialogThemePaletteService.GetCardStrokeBrush();
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, Foreground = primaryTextBrush });

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "ModDownloadDetailPage_DownloadDialog_PrimaryButtonText".GetLocalized(),
            SecondaryButtonText = "ModDownloadDetailPage_DownloadDialog_SecondaryButtonText".GetLocalized(),
            CloseButtonText = "ModDownloadDetailPage_DownloadDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.None,
        };

        var deps = dependencyProjects?.ToList();
        if (deps != null && deps.Count > 0)
        {
            if (isLoadingDependencies)
            {
                panel.Children.Add(new ProgressRing
                {
                    IsActive = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Height = 32,
                    Width = 32,
                    Margin = new Thickness(0, 8, 0, 8),
                });
            }
            else
            {
                var depsPanel = new StackPanel { Spacing = 8 };
                foreach (dynamic dep in deps)
                {
                    string projectId = dep.ProjectId;
                    string depTitle = dep.Title;
                    string description = dep.DisplayDescription;
                    string iconUrl = dep.IconUrl;

                    var cardContent = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 356,
                    };

                    var iconBorder = new Border { CornerRadius = new CornerRadius(4), Width = 40, Height = 40 };
                    var iconImage = new Image { Width = 40, Height = 40, Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill };
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        iconImage.Source = new BitmapImage(new Uri(iconUrl));
                    }

                    iconBorder.Child = iconImage;
                    cardContent.Children.Add(iconBorder);

                    var textPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Width = 300 };
                    textPanel.Children.Add(new TextBlock { Text = depTitle, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = primaryTextBrush });
                    textPanel.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = secondaryTextBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.WrapWholeWords });
                    cardContent.Children.Add(textPanel);

                    var button = new Button
                    {
                        Content = cardContent,
                        Background = cardBgBrush,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12),
                        BorderThickness = new Thickness(1),
                        BorderBrush = cardStrokeBrush,
                        Width = 380,
                        HorizontalAlignment = HorizontalAlignment.Left,
                    };

                    var subtleFill = _dialogThemePaletteService.GetSubtleFillBrush();
                    button.Resources["ButtonBackgroundPointerOver"] = subtleFill;
                    button.Resources["ButtonBackgroundPressed"] = subtleFill;

                    var capturedId = projectId;
                    button.Click += (_, _) =>
                    {
                        dialog.Hide();
                        onDependencyClick?.Invoke(capturedId);
                    };

                    depsPanel.Children.Add(button);
                }

                panel.Children.Add(depsPanel);
            }
        }

        return await _dialogHostService.ShowAsync(dialog);
    }

    public async Task<T?> ShowListSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> displayMemberFunc,
        Func<T, double>? opacityFunc = null,
        string? tip = null,
        string primaryButtonText = "确认",
        string closeButtonText = "取消") where T : class
    {
        var primaryTextBrush = _dialogThemePaletteService.GetPrimaryTextBrush();
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, Foreground = primaryTextBrush });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300,
        };

        var itemsList = items.ToList();
        foreach (var item in itemsList)
        {
            var grid = new Grid { Padding = new Thickness(8) };
            var textBlock = new TextBlock { Text = displayMemberFunc(item), Foreground = primaryTextBrush };
            if (opacityFunc != null)
            {
                textBlock.Opacity = opacityFunc(item);
            }

            grid.Children.Add(textBlock);
            listView.Items.Add(new ListViewItem { Content = grid, Tag = item });
        }

        if (listView.Items.Count > 0)
        {
            listView.SelectedIndex = 0;
        }

        panel.Children.Add(listView);

        if (!string.IsNullOrEmpty(tip))
        {
            panel.Children.Add(new TextBlock
            {
                Text = tip,
                FontSize = 12,
                Foreground = secondaryTextBrush,
            });
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary && listView.SelectedItem is ListViewItem selectedItem)
        {
            return selectedItem.Tag as T;
        }

        return null;
    }

    public async Task<T?> ShowModVersionSelectionDialogAsync<T>(
        string title,
        string instruction,
        IEnumerable<T> items,
        Func<T, string> versionNumberFunc,
        Func<T, string> versionTypeFunc,
        Func<T, string> releaseDateFunc,
        Func<T, string> fileNameFunc,
        Func<T, string?>? resourceTypeTagFunc = null,
        string primaryButtonText = "安装",
        string closeButtonText = "取消") where T : class
    {
        var primaryTextBrush = _dialogThemePaletteService.GetPrimaryTextBrush();
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();
        var tertiaryTextBrush = _dialogThemePaletteService.GetTertiaryTextBrush();
        var cardBgBrush = _dialogThemePaletteService.GetCardBackgroundBrush();
        var cardStrokeBrush = _dialogThemePaletteService.GetCardStrokeBrush();
        var subtleFillBrush = _dialogThemePaletteService.GetSubtleFillBrush();
        var accentFillBrush = _dialogThemePaletteService.GetAccentFillBrush();
        var textOnAccentBrush = _dialogThemePaletteService.GetTextOnAccentBrush();

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = instruction, FontSize = 14, TextWrapping = TextWrapping.WrapWholeWords, Foreground = primaryTextBrush });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 400,
        };

        foreach (var item in items)
        {
            var card = new Border
            {
                Background = cardBgBrush,
                BorderBrush = cardStrokeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 4),
            };

            var cardPanel = new StackPanel { Spacing = 4 };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerRow.Children.Add(new TextBlock { Text = versionNumberFunc(item), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = primaryTextBrush });

            var typeBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = subtleFillBrush,
            };
            var versionType = versionTypeFunc(item);
            typeBadge.Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(versionType) ? versionType : char.ToUpper(versionType[0]) + versionType[1..],
                FontSize = 11,
                Foreground = secondaryTextBrush,
            };
            headerRow.Children.Add(typeBadge);

            if (resourceTypeTagFunc != null)
            {
                var tag = resourceTypeTagFunc(item);
                if (!string.IsNullOrEmpty(tag))
                {
                    var resourceBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Background = accentFillBrush,
                    };
                    resourceBadge.Child = new TextBlock
                    {
                        Text = tag,
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = textOnAccentBrush,
                    };
                    headerRow.Children.Add(resourceBadge);
                }
            }

            cardPanel.Children.Add(headerRow);
            cardPanel.Children.Add(new TextBlock
            {
                Text = $"发布日期: {releaseDateFunc(item)}",
                FontSize = 12,
                Foreground = secondaryTextBrush,
            });
            cardPanel.Children.Add(new TextBlock
            {
                Text = fileNameFunc(item),
                FontSize = 11,
                Foreground = tertiaryTextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            card.Child = cardPanel;
            listView.Items.Add(new ListViewItem
            {
                Content = card,
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 6),
            });
        }

        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary && listView.SelectedItem is ListViewItem selectedItem)
        {
            return selectedItem.Tag as T;
        }

        return null;
    }

    public async Task<List<XianYuLauncher.Models.UpdatableResourceItem>?> ShowUpdatableResourcesSelectionDialogAsync(IEnumerable<XianYuLauncher.Models.UpdatableResourceItem> availableUpdates)
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 600, MinWidth = 400 };
        panel.Children.Add(new TextBlock
        {
            Text = "VersionManagerPage_UpdatableResourcesUpdateDialog_InstructionText".GetLocalized(),
            FontSize = 14,
        });

        var itemsList = availableUpdates.ToList();
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 350,
            ItemsSource = itemsList,
            ItemTemplate = Application.Current.Resources["UpdatableResourceSelectionItemTemplate"] as DataTemplate,
        };

        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = "VersionManagerPage_UpdatableResourcesUpdateDialog_Title".GetLocalized(),
            Content = panel,
            PrimaryButtonText = "VersionManagerPage_UpdatableResourcesUpdateDialog_PrimaryButtonText".GetLocalized(),
            CloseButtonText = "VersionManagerPage_UpdatableResourcesUpdateDialog_CloseButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            return itemsList.Where(x => x.IsSelected).ToList();
        }

        return null;
    }

    public async Task ShowMoveResultDialogAsync(
        IEnumerable<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult> moveResults,
        string title,
        string instruction)
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 600, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = instruction,
            FontSize = 14,
        });

        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            MaxHeight = 400,
            ItemsSource = moveResults?.ToList() ?? new List<XianYuLauncher.Features.VersionManagement.ViewModels.MoveModResult>(),
            ItemTemplate = Application.Current.Resources["MoveResultDialogItemTemplate"] as DataTemplate,
        };

        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "VersionManagerPage_MoveResultDialog_PrimaryButtonText".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
        };

        await _dialogHostService.ShowAsync(dialog);
    }

    public async Task ShowPublishersListDialogAsync(
        IEnumerable<PublisherDialogItem> publishers,
        bool isLoading,
        string title = "所有发布者",
        string closeButtonText = "关闭")
    {
        var stackPanel = new StackPanel { Spacing = 12, Width = 420, MaxHeight = 500 };
        var secondaryTextBrush = _dialogThemePaletteService.GetSecondaryTextBrush();

        if (isLoading)
        {
            stackPanel.Children.Add(new ProgressRing
            {
                IsActive = true,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }
        else
        {
            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 420,
                IsItemClickEnabled = false,
            };

            foreach (var publisher in publishers)
            {
                var rowGrid = new Grid { Padding = new Thickness(8) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var avatarContainer = new Grid { Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(avatarContainer, 0);

                var hasAvatar = !string.IsNullOrWhiteSpace(publisher.AvatarUrl)
                    && !publisher.AvatarUrl.Contains("Placeholder", StringComparison.OrdinalIgnoreCase);

                if (hasAvatar)
                {
                    var avatarBorder = new Border
                    {
                        Width = 40,
                        Height = 40,
                        CornerRadius = new CornerRadius(20),
                    };

                    try
                    {
                        avatarBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(publisher.AvatarUrl, UriKind.RelativeOrAbsolute)),
                            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                        };
                    }
                    catch
                    {
                        hasAvatar = false;
                    }

                    if (hasAvatar)
                    {
                        avatarContainer.Children.Add(avatarBorder);
                    }
                }

                if (!hasAvatar)
                {
                    var placeholder = new Border
                    {
                        Width = 40,
                        Height = 40,
                        CornerRadius = new CornerRadius(20),
                        Background = _dialogThemePaletteService.GetCardBackgroundBrush(),
                    };
                    placeholder.Child = new FontIcon
                    {
                        Glyph = "\uE77B",
                        FontSize = 20,
                        FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                        Foreground = secondaryTextBrush,
                    };
                    avatarContainer.Children.Add(placeholder);
                }

                var textPanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(textPanel, 1);
                textPanel.Children.Add(new TextBlock
                {
                    Text = publisher.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = _dialogThemePaletteService.GetPrimaryTextBrush(),
                });
                textPanel.Children.Add(new TextBlock
                {
                    Text = publisher.Role,
                    FontSize = 12,
                    Foreground = secondaryTextBrush,
                });

                rowGrid.Children.Add(avatarContainer);
                rowGrid.Children.Add(textPanel);
                listView.Items.Add(rowGrid);
            }

            stackPanel.Children.Add(listView);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = stackPanel,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.None,
        };

        await _dialogHostService.ShowAsync(dialog);
    }
}