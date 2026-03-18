using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XianYuLauncher.ViewModels
{
    /// <summary>
    /// Minecraft版本比较器，支持语义化版本排序
    /// </summary>
    internal class MinecraftVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == y) return 0;
            if (string.IsNullOrEmpty(x)) return -1;
            if (string.IsNullOrEmpty(y)) return 1;

            // 尝试解析为版本号
            var xParts = ParseVersion(x);
            var yParts = ParseVersion(y);

            // 比较每个部分
            int maxLength = Math.Max(xParts.Length, yParts.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int xPart = i < xParts.Length ? xParts[i] : 0;
                int yPart = i < yParts.Length ? yParts[i] : 0;

                if (xPart != yPart)
                {
                    return xPart.CompareTo(yPart);
                }
            }

            // 如果数字部分相同，按字符串比较（处理后缀如 -pre, -rc）
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private int[] ParseVersion(string version)
        {
            // 提取版本号的数字部分（如 "1.21.10" -> [1, 21, 10]）
            var match = Regex.Match(version, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (match.Success)
            {
                var parts = new List<int>();
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (match.Groups[i].Success && int.TryParse(match.Groups[i].Value, out int part))
                    {
                        parts.Add(part);
                    }
                }

                return parts.ToArray();
            }

            // 如果无法解析，返回空数组
            return Array.Empty<int>();
        }
    }
}
