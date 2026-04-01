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
    public static ModpackInstallNameValidationResult Validate(string? versionName, string minecraftPath)
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
        catch
        {
            // 安装阶段会再次做最终校验，这里保持与输入弹窗一致的宽松预检查。
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