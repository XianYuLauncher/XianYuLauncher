using System.Collections.ObjectModel;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using Serilog;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Features.Dialogs.Contracts;
using XianYuLauncher.Features.Dialogs.Models;
using XianYuLauncher.Helpers;

namespace XianYuLauncher.Features.Dialogs.Services;

public sealed class ProfileDialogService : IProfileDialogService
{
    private readonly HttpClient _httpClient = new();
    private readonly IContentDialogHostService _dialogHostService;
    private readonly IUiDispatcher _uiDispatcher;

    public ProfileDialogService(IContentDialogHostService dialogHostService, IUiDispatcher uiDispatcher)
    {
        _dialogHostService = dialogHostService ?? throw new ArgumentNullException(nameof(dialogHostService));
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", XianYuLauncher.Core.Helpers.VersionHelper.GetUserAgent());
    }

    public async Task<XianYuLauncher.Core.Services.ExternalProfile?> ShowProfileSelectionDialogAsync(List<XianYuLauncher.Core.Services.ExternalProfile> profiles, string authServer)
    {
        var items = new ObservableCollection<ProfileSelectionItem>();

        BitmapImage defaultAvatar;
        try
        {
            defaultAvatar = await ProfileAvatarImageHelper.CreateDefaultProfileAvatarAsync();
        }
        catch
        {
            defaultAvatar = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
        }

        foreach (var profile in profiles)
        {
            items.Add(new ProfileSelectionItem
            {
                Id = profile.Id,
                Name = profile.Name,
                OriginalProfile = profile,
                Avatar = defaultAvatar,
            });
        }

        Log.Information("[Avatar.ProfileDialogService] 外置角色选择对话框，AuthServer: {AuthServer}, 角色数: {Count}", authServer ?? "(null)", profiles.Count);

        var avatarLoadCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            if (string.IsNullOrEmpty(authServer))
            {
                Log.Warning("[Avatar.ProfileDialogService] AuthServer 为空，跳过头像加载");
                return;
            }

            foreach (var item in items)
            {
                avatarLoadCts.Token.ThrowIfCancellationRequested();

                try
                {
                    var server = authServer;
                    if (!server.EndsWith('/'))
                    {
                        server += "/";
                    }

                    var sessionUrl = $"{server}sessionserver/session/minecraft/profile/{item.Id}";
                    Log.Information("[Avatar.ProfileDialogService] 加载角色 {Name} 头像，Session URL: {Url}", item.Name, sessionUrl);

                    var response = await _httpClient.GetStringAsync(sessionUrl, avatarLoadCts.Token);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(response);

                    var properties = data?["properties"] as JArray;
                    string? textureProperty = properties
                        ?.OfType<JObject>()
                        .FirstOrDefault(prop => string.Equals(prop["name"]?.ToString(), "textures", StringComparison.Ordinal))?
                        ["value"]?.ToString();

                    if (string.IsNullOrEmpty(textureProperty))
                    {
                        Log.Warning("[Avatar.ProfileDialogService] 角色 {Name} Session API 无 textures，URL: {Url}", item.Name, sessionUrl);
                    }

                    if (!string.IsNullOrEmpty(textureProperty))
                    {
                        var jsonBytes = Convert.FromBase64String(textureProperty);
                        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                        var textureData = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json);
                        string? skinUrl = textureData?["textures"]?["SKIN"]?["url"]?.ToString();

                        if (!string.IsNullOrEmpty(skinUrl))
                        {
                            Log.Debug("[Avatar.ProfileDialogService] 角色 {Name} 皮肤 URL: {SkinUrl}", item.Name, skinUrl);
                            var skinBytes = await _httpClient.GetByteArrayAsync(skinUrl, avatarLoadCts.Token);

                            _uiDispatcher.EnqueueAsync(async () =>
                            {
                                var processedAvatar = await ProcessAvatarBytesAsync(skinBytes);
                                if (processedAvatar != null)
                                {
                                    item.Avatar = processedAvatar;
                                }
                            }).Observe("ProfileDialogService.LoadProfileAvatar");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("[Avatar.ProfileDialogService] 头像加载任务已取消");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Avatar.ProfileDialogService] 加载角色 {Name} 头像失败，AuthServer: {AuthServer}", item.Name, authServer);
                }
            }
        });

        var itemTemplate = Application.Current.Resources["ProfileSelectionItemTemplate"] as DataTemplate;
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = items,
            SelectedIndex = 0,
            MaxHeight = 300,
            ItemTemplate = itemTemplate,
        };

        if (profiles.Count == 0)
        {
            listView.SelectedIndex = -1;
        }

        var dialog = new ContentDialog
        {
            Title = "ProfilePage_ExternalLoginDialog_SelectProfileTitle".GetLocalized(),
            PrimaryButtonText = "ProfilePage_ExternalLoginDialog_ConfirmButton".GetLocalized(),
            CloseButtonText = "ProfilePage_ExternalLoginDialog_CancelButton".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            Content = listView,
        };
        dialog.Closed += (_, _) => avatarLoadCts.Cancel();

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            var selectedItem = listView.SelectedItem as ProfileSelectionItem;
            return selectedItem?.OriginalProfile;
        }

        return null;
    }

    public async Task<MinecraftProfile?> ShowLauncherProfileSelectionDialogAsync(
        List<MinecraftProfile> profiles,
        string title,
        string primaryButtonText,
        string closeButtonText)
    {
        var items = new ObservableCollection<ProfileSelectionItem>();

        BitmapImage defaultAvatar;
        try
        {
            defaultAvatar = await ProfileAvatarImageHelper.CreateDefaultProfileAvatarAsync();
        }
        catch
        {
            defaultAvatar = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png"));
        }

        foreach (var profile in profiles
                     .OrderByDescending(profile => profile.IsActive)
                     .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(new ProfileSelectionItem
            {
                Id = profile.Id,
                Name = profile.Name,
                Avatar = defaultAvatar,
            });
        }

        var itemTemplate = Application.Current.Resources["ProfileSelectionItemTemplate"] as DataTemplate;
        var listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = items,
            SelectedIndex = items.Count > 0 ? 0 : -1,
            MaxHeight = 300,
            ItemTemplate = itemTemplate,
        };

        var activeProfileId = profiles.FirstOrDefault(profile => profile.IsActive)?.Id;
        if (!string.IsNullOrWhiteSpace(activeProfileId))
        {
            var activeItem = items.FirstOrDefault(item => string.Equals(item.Id, activeProfileId, StringComparison.OrdinalIgnoreCase));
            if (activeItem != null)
            {
                listView.SelectedItem = activeItem;
            }
        }

        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            Content = listView,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var selectedItem = listView.SelectedItem as ProfileSelectionItem;
        if (selectedItem == null)
        {
            return null;
        }

        return profiles.FirstOrDefault(profile => string.Equals(profile.Id, selectedItem.Id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string? title = null,
        string? instruction = null,
        string? browserDescription = null,
        string? deviceCodeDescription = null,
        string? browserButtonText = null,
        string? deviceCodeButtonText = null,
        string? cancelButtonText = null)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Dialog_LoginMethod_Title".GetLocalized() : title;
        var resolvedInstruction = string.IsNullOrWhiteSpace(instruction) ? "Dialog_LoginMethod_Instruction".GetLocalized() : instruction;
        var resolvedBrowserDescription = string.IsNullOrWhiteSpace(browserDescription) ? "Dialog_LoginMethod_BrowserDesc".GetLocalized() : browserDescription;
        var resolvedDeviceCodeDescription = string.IsNullOrWhiteSpace(deviceCodeDescription) ? "Dialog_LoginMethod_DeviceCodeDesc".GetLocalized() : deviceCodeDescription;
        var resolvedBrowserButtonText = string.IsNullOrWhiteSpace(browserButtonText) ? "Dialog_LoginMethod_BrowserButton".GetLocalized() : browserButtonText;
        var resolvedDeviceCodeButtonText = string.IsNullOrWhiteSpace(deviceCodeButtonText) ? "Dialog_LoginMethod_DeviceCodeButton".GetLocalized() : deviceCodeButtonText;
        var resolvedCancelButtonText = string.IsNullOrWhiteSpace(cancelButtonText) ? "Msg_Cancel".GetLocalized() : cancelButtonText;

        var dialog = new ContentDialog
        {
            Title = resolvedTitle,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = resolvedInstruction, Margin = new Thickness(0, 0, 0, 10) },
                    new TextBlock { Text = resolvedBrowserDescription, Opacity = 0.8, FontSize = 12 },
                    new TextBlock { Text = resolvedDeviceCodeDescription, Opacity = 0.8, FontSize = 12 },
                },
            },
            PrimaryButtonText = resolvedBrowserButtonText,
            SecondaryButtonText = resolvedDeviceCodeButtonText,
            CloseButtonText = resolvedCancelButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => LoginMethodSelectionResult.Browser,
            ContentDialogResult.Secondary => LoginMethodSelectionResult.DeviceCode,
            _ => LoginMethodSelectionResult.Cancel,
        };
    }

    public async Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Dialog_SkinModel_Title".GetLocalized(),
            Content = "Dialog_SkinModel_Content".GetLocalized(),
            PrimaryButtonText = "Steve",
            SecondaryButtonText = "Alex",
            CloseButtonText = "Msg_Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.None,
        };

        var result = await _dialogHostService.ShowAsync(dialog);
        return result switch
        {
            ContentDialogResult.Primary => SkinModelSelectionResult.Steve,
            ContentDialogResult.Secondary => SkinModelSelectionResult.Alex,
            _ => SkinModelSelectionResult.Cancel,
        };
    }

    private static async Task<BitmapImage?> ProcessAvatarBytesAsync(byte[] skinBytes)
    {
        try
        {
            return await ProfileAvatarImageHelper.CreateProfileAvatarFromSkinAsync(skinBytes, outputSize: 32, includeOverlay: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Avatar.ProfileDialogService] 处理皮肤头像失败，将回退到默认头像。");
            return null;
        }
    }
}