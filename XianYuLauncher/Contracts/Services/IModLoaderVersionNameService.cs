namespace XianYuLauncher.Contracts.Services;

public interface IModLoaderVersionNameService
{
    /// <summary>
    /// 生成版本名称
    /// </summary>
    /// <param name="minecraftVersion">Minecraft 版本</param>
    /// <param name="modLoaderType">ModLoader 类型</param>
    /// <param name="modLoaderVersion">ModLoader 版本</param>
    /// <param name="isOptifineSelected">是否选择了 OptiFine</param>
    /// <param name="optifineVersion">OptiFine 版本</param>
    /// <param name="isLiteLoaderSelected">是否选择了 LiteLoader</param>
    /// <param name="liteLoaderVersion">LiteLoader 版本</param>
    /// <returns>生成的版本名称</returns>
    string GenerateVersionName(string minecraftVersion, string? modLoaderType, string? modLoaderVersion, bool isOptifineSelected, string? optifineVersion, bool isLiteLoaderSelected, string? liteLoaderVersion);

    /// <summary>
    /// 验证版本名称是否合法（检查是否为空、是否已存在等）
    /// </summary>
    /// <param name="versionName">版本名称</param>
    /// <returns>IsVersionNameValid: 是否合法, ErrorMessage: 错误信息</returns>
    (bool IsValid, string ErrorMessage) ValidateVersionName(string versionName);
}
