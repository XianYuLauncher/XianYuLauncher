using System.IO;

namespace XianYuLauncher.Core.Helpers;

public enum ModpackInstallNameValidationError
{
    None,
    Empty,
    InvalidChars,
    ReservedDeviceName,
    TrailingSpaceOrDot,
    TooLong,
    AlreadyExists,
}

public readonly record struct ModpackInstallNameValidationResult(
    bool IsValid,
    string NormalizedName,
    ModpackInstallNameValidationError Error,
    int MaxSafeLength);

public static class ModpackInstallNameValidationHelper
{
    public static ModpackInstallNameValidationResult Validate(
        string? versionName,
        string minecraftPath,
        bool suppressDirectoryCheckExceptions = true)
    {
        var baseResult = VersionNameValidationHelper.ValidateVersionName(versionName ?? string.Empty, minecraftPath);
        if (!baseResult.IsValid)
        {
            return new ModpackInstallNameValidationResult(
                false,
                baseResult.NormalizedName,
                MapError(baseResult.Error),
                baseResult.MaxSafeLength);
        }

        try
        {
            string versionDirectory = Path.Combine(minecraftPath, MinecraftPathConsts.Versions, baseResult.NormalizedName);
            if (Directory.Exists(versionDirectory))
            {
                return new ModpackInstallNameValidationResult(
                    false,
                    baseResult.NormalizedName,
                    ModpackInstallNameValidationError.AlreadyExists,
                    baseResult.MaxSafeLength);
            }
        }
        catch when (suppressDirectoryCheckExceptions)
        {
            // UI 预校验允许在极端文件系统异常时退化为“无法提前发现重名实例”；
            // 安装阶段应传 suppressDirectoryCheckExceptions=false，避免吞掉最终校验异常。
        }

        return new ModpackInstallNameValidationResult(
            true,
            baseResult.NormalizedName,
            ModpackInstallNameValidationError.None,
            baseResult.MaxSafeLength);
    }

    private static ModpackInstallNameValidationError MapError(VersionNameValidationError error)
    {
        return error switch
        {
            VersionNameValidationError.Empty => ModpackInstallNameValidationError.Empty,
            VersionNameValidationError.InvalidChars => ModpackInstallNameValidationError.InvalidChars,
            VersionNameValidationError.ReservedDeviceName => ModpackInstallNameValidationError.ReservedDeviceName,
            VersionNameValidationError.TrailingSpaceOrDot => ModpackInstallNameValidationError.TrailingSpaceOrDot,
            VersionNameValidationError.TooLong => ModpackInstallNameValidationError.TooLong,
            _ => ModpackInstallNameValidationError.None,
        };
    }
}