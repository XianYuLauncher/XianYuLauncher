using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

namespace XianYuLauncher.Models.VersionManagement;

public partial class ServerItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string? _iconBase64;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private BitmapImage? _icon;

    public bool HasIcon => Icon != null;

    [ObservableProperty]
    private string? _playerCount = "- / -";

    public async Task DecodeIconAsync()
    {
        if (string.IsNullOrEmpty(IconBase64)) return;

        try
        {
            // Base64 format usually comes as "data:image/png;base64,..." in some contexts, 
            // but in servers.dat it might just be the base64 string or verified. 
            // Standard NBT servers.dat usually stores it strictly as base64 string 
            // but often includes data URI prefix "data:image/png;base64,".
            
            var base64Data = IconBase64;
            if (base64Data.StartsWith("data:image/png;base64,"))
            {
                base64Data = base64Data.Substring("data:image/png;base64,".Length);
            }

            var bytes = Convert.FromBase64String(base64Data);
            
            // 在UI线程创建BitmapImage
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                Icon = bitmap;
            });
        }
        catch
        {
            // Decoding failed, icon will remain null (default)
        }
    }
}
