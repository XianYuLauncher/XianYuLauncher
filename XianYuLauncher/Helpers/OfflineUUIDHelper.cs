using System.Security.Cryptography;
using System.Text;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 离线玩家 UUID 生成助手
    /// 使用 Minecraft 官方标准算法生成离线 UUID
    /// </summary>
    public static class OfflineUUIDHelper
    {
        /// <summary>
        /// 生成和 Minecraft 官方完全一致的离线 UUID
        /// 遵循 Version 3 UUID + Minecraft 字节序规则
        /// </summary>
        /// <param name="username">玩家用户名</param>
        /// <returns>符合官方标准的离线 UUID</returns>
        public static Guid GenerateMinecraftOfflineUUID(string username)
        {
            // 1. 空值校验
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("用户名不能为空", nameof(username));
            }

            // 2. 生成 MD5 哈希（基础步骤）
            byte[] inputBytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
            var hashBytes = MD5.HashData(inputBytes);

            // 3. 关键修正：按 Minecraft/UUID v3 规则调整字节数组
            // 3.1 调整第 6 字节：设置 Version 3（高 4 位为 0x3）
            hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | 0x30);
            // 3.2 调整第 8 字节：设置 RFC 4122 Variant（高 2 位为 0x8）
            hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80);

            // 3.3 调整字节序（前 8 字节按 UUID 标准的大端/小端转换）
            // 第一段（8 位）：反转字节序
            Array.Reverse(hashBytes, 0, 4);
            // 第二段（4 位）：反转字节序
            Array.Reverse(hashBytes, 4, 2);
            // 第三段（4 位）：反转字节序
            Array.Reverse(hashBytes, 6, 2);
            // 后两段（12 位）：保持原样

            // 4. 生成最终的 Guid
            return new Guid(hashBytes);
        }
        
        /// <summary>
        /// 生成和 Minecraft 官方完全一致的离线 UUID 字符串
        /// </summary>
        /// <param name="username">玩家用户名</param>
        /// <returns>符合官方标准的离线 UUID 字符串（不带连字符）</returns>
        public static string GenerateMinecraftOfflineUUIDString(string username)
        {
            return GenerateMinecraftOfflineUUID(username).ToString("N");
        }
    }
}