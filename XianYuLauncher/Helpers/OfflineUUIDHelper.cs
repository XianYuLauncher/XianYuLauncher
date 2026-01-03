using System;
using System.Text;

namespace XianYuLauncher.Helpers
{
    /// <summary>
    /// 离线玩家UUID生成助手
    /// 使用Minecraft官方标准算法生成离线UUID
    /// </summary>
    public static class OfflineUUIDHelper
    {
        /// <summary>
        /// 生成和Minecraft官方完全一致的离线UUID
        /// 遵循Version 3 UUID + Minecraft字节序规则
        /// </summary>
        /// <param name="username">玩家用户名</param>
        /// <returns>符合官方标准的离线UUID</returns>
        public static Guid GenerateMinecraftOfflineUUID(string username)
        {
            // 1. 空值校验
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("用户名不能为空", nameof(username));
            }

            // 2. 生成MD5哈希（基础步骤）
            byte[] hashBytes;
            byte[] inputBytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                hashBytes = md5.ComputeHash(inputBytes);
            }

            // 3. 关键修正：按Minecraft/UUID v3规则调整字节数组
            // 3.1 调整第6字节：设置Version 3（高4位为0x3）
            hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | 0x30);
            // 3.2 调整第8字节：设置RFC 4122 Variant（高2位为0x8）
            hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80);

            // 3.3 调整字节序（前8字节按UUID标准的大端/小端转换）
            // 第一段（8位）：反转字节序
            Array.Reverse(hashBytes, 0, 4);
            // 第二段（4位）：反转字节序
            Array.Reverse(hashBytes, 4, 2);
            // 第三段（4位）：反转字节序
            Array.Reverse(hashBytes, 6, 2);
            // 后两段（12位）：保持原样

            // 4. 生成最终的Guid
            return new Guid(hashBytes);
        }
        
        /// <summary>
        /// 生成和Minecraft官方完全一致的离线UUID字符串
        /// </summary>
        /// <param name="username">玩家用户名</param>
        /// <returns>符合官方标准的离线UUID字符串（不带连字符）</returns>
        public static string GenerateMinecraftOfflineUUIDString(string username)
        {
            return GenerateMinecraftOfflineUUID(username).ToString("N");
        }
    }
}