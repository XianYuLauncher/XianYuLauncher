using System.Security.Cryptography;
using System.Text;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// 应用配置数据处理工具
/// </summary>
public static class SecretProtector
{
    /// <summary>
    /// 获取应用标识符
    /// </summary>
    private static string GetAppIdentifier()
    {
        var part1 = AppMetadata.ProjectIdentifier;
        var part2 = BuildConfiguration.TargetPlatform;
        var part3 = VersionMetadata.ReleaseYear;
        var part4 = AppMetadata.ComponentSuffix;

        var combined = $"{part1}_{part2}_{part3}_{part4}_Key_V1";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 编码配置数据
    /// </summary>
    public static string Encrypt(string plainText)
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;

            var keyBytes = Convert.FromBase64String(GetAppIdentifier());
            aes.Key = keyBytes;
            aes.IV = new byte[16];

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(cipherBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretProtector] 编码失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解码配置数据
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;

            var keyBytes = Convert.FromBase64String(GetAppIdentifier());
            aes.Key = keyBytes;
            aes.IV = new byte[16];

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretProtector] 解码失败: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// 应用元数据
/// </summary>
internal static class AppMetadata
{
    public const string ProjectIdentifier = "XianYu";
    public const string ComponentSuffix = "SpiritStudio";
}

/// <summary>
/// 构建配置
/// </summary>
internal static class BuildConfiguration
{
    public const string TargetPlatform = "Launcher";
}

/// <summary>
/// 版本元数据
/// </summary>
internal static class VersionMetadata
{
    public const string ReleaseYear = "1.5.5_dev03";
}
