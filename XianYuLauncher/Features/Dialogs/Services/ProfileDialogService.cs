using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using Serilog;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Contracts.Services;
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
            defaultAvatar = await ProcessLocalSteveAvatarAsync();
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
                                try
                                {
                                    var processedAvatar = await ProcessAvatarBytesAsync(skinBytes);
                                    if (processedAvatar != null)
                                    {
                                        item.Avatar = processedAvatar;
                                    }
                                }
                                catch
                                {
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

    public async Task<LoginMethodSelectionResult> ShowLoginMethodSelectionDialogAsync(
        string title = "选择登录方式",
        string instruction = "请选择您喜欢的登录方式：",
        string browserDescription = "• 浏览器登录：打开系统默认浏览器进行登录 (推荐)",
        string deviceCodeDescription = "• 设备代码登录：获取代码后手动访问网页输入",
        string browserButtonText = "浏览器登录",
        string deviceCodeButtonText = "设备代码登录",
        string cancelButtonText = "取消")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = instruction, Margin = new Thickness(0, 0, 0, 10) },
                    new TextBlock { Text = browserDescription, Opacity = 0.8, FontSize = 12 },
                    new TextBlock { Text = deviceCodeDescription, Opacity = 0.8, FontSize = 12 },
                },
            },
            PrimaryButtonText = browserButtonText,
            SecondaryButtonText = deviceCodeButtonText,
            CloseButtonText = cancelButtonText,
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

    public async Task<SkinModelSelectionResult> ShowSkinModelSelectionDialogAsync(
        string title = "选择皮肤模型",
        string content = "请选择此皮肤适用的人物模型",
        string steveButtonText = "Steve",
        string alexButtonText = "Alex",
        string cancelButtonText = "取消")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = steveButtonText,
            SecondaryButtonText = alexButtonText,
            CloseButtonText = cancelButtonText,
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

    private static async Task<BitmapImage> ProcessLocalSteveAvatarAsync()
    {
        var device = CanvasDevice.GetSharedDevice();
        var uri = new Uri("ms-appx:///Assets/Icons/Avatars/Steve.png");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);

        using var stream = await file.OpenReadAsync();
        var originalBitmap = await CanvasBitmap.LoadAsync(device, stream);

        var renderTarget = new CanvasRenderTarget(device, 32, 32, 96);
        using (var drawingSession = renderTarget.CreateDrawingSession())
        {
            PixelArtRenderHelper.SetAliased(drawingSession);
            PixelArtRenderHelper.DrawNearestNeighbor(
                drawingSession,
                originalBitmap,
                new Windows.Foundation.Rect(0, 0, 32, 32),
                originalBitmap.Bounds);
        }

        using var outputStream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(outputStream, CanvasBitmapFileFormat.Png);
        outputStream.Seek(0);

        var bitmapImage = new BitmapImage();
        await bitmapImage.SetSourceAsync(outputStream);
        return bitmapImage;
    }

    private static async Task<BitmapImage?> ProcessAvatarBytesAsync(byte[] skinBytes)
    {
        try
        {
            var device = CanvasDevice.GetSharedDevice();
            using var stream = new MemoryStream(skinBytes);
            var originalBitmap = await CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream());
            return await SkinAvatarHelper.CropHeadFromSkinAsync(originalBitmap, outputSize: 32, includeOverlay: true);
        }
        catch
        {
            return null;
        }
    }
}