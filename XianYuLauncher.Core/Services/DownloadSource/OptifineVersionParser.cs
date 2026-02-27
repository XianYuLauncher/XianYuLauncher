namespace XianYuLauncher.Core.Services.DownloadSource;

/// <summary>
/// 辅助解析 OptiFine 版本号，提取 type 和 patch 用于构建下载 URL。
/// 当前支持格式：{mcVersion}-HD_U_{patch}（如 1.19.2-HD_U_H9）或 {mcVersion}_HD_U_{patch}
/// </summary>
internal static class OptifineVersionParser
{
    /// <summary>
    /// 从 OptiFine 版本字符串中解析出 type 和 patch。
    /// </summary>
    /// <param name="optifineVersion">OptiFine 版本字符串（如 "1.19.2-HD_U_H9"）</param>
    /// <param name="minecraftVersion">Minecraft 版本（用于截取后缀）</param>
    /// <param name="type">解析出的 type（如 "HD_U"），解析失败时为 null</param>
    /// <param name="patch">解析出的 patch（如 "H9"），解析失败时为 null</param>
    /// <returns>解析是否成功</returns>
    public static bool TryParse(string optifineVersion, string minecraftVersion, out string? type, out string? patch)
    {
        type = null;
        patch = null;

        // 基于 minecraftVersion 截取后缀部分（如 "HD_U_H9"）
        string suffix = optifineVersion;
        if (optifineVersion.StartsWith(minecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            var remaining = optifineVersion[minecraftVersion.Length..];
            if (remaining.StartsWith("-") || remaining.StartsWith("_"))
                remaining = remaining[1..];
            if (!string.IsNullOrEmpty(remaining))
                suffix = remaining;
        }

        // 期望格式: HD_U_H9（至少3段，以 HD 开头）
        var parts = suffix.Split('_');
        if (parts.Length >= 3 && parts[0].Equals("HD", StringComparison.OrdinalIgnoreCase))
        {
            type = $"{parts[0]}_{parts[1]}";
            patch = parts[2];
            return true;
        }

        return false;
    }
}
