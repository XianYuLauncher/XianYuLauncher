using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using XianYuLauncher.Models.VersionManagement;

namespace XianYuLauncher.ViewModels;

internal static class VersionManagementMapOps
{
    internal enum RenameMapStatus
    {
        Success,
        NoChange,
        NameExists
    }

    internal sealed class RenameMapResult
    {
        public RenameMapStatus Status { get; init; }
        public string? NewPath { get; init; }
    }

    public static RenameMapResult RenameMap(MapInfo map, string mapRenameInput)
    {
        var oldPath = map.FilePath;
        var parentPath = Path.GetDirectoryName(oldPath);
        var newPath = Path.Combine(parentPath!, mapRenameInput);

        if (Directory.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameMapResult
            {
                Status = RenameMapStatus.NameExists,
                NewPath = newPath
            };
        }

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameMapResult
            {
                Status = RenameMapStatus.NoChange,
                NewPath = oldPath
            };
        }

        Directory.Move(oldPath, newPath);
        map.FilePath = newPath;
        map.FileName = Path.GetFileName(newPath);
        map.Name = mapRenameInput;

        return new RenameMapResult
        {
            Status = RenameMapStatus.Success,
            NewPath = newPath
        };
    }

    public static async Task DeleteMapDirectoryAsync(string mapPath)
    {
        if (!Directory.Exists(mapPath))
        {
            return;
        }

        var folder = await StorageFolder.GetFolderFromPathAsync(mapPath);
        await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
    }

    public static async Task ExportMapAsZipAsync(string mapPath, string targetZipPath)
    {
        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(mapPath, targetZipPath, CompressionLevel.Optimal, false);
        });
    }
}
