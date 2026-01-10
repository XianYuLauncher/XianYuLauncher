using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XianYuLauncher.Core.Helpers;

/// <summary>
/// CurseForge Fingerprint 计算工具类
/// 使用 MurmurHash2 算法，并跳过空白字符（空格、制表符、换行符、回车符）
/// </summary>
public static class CurseForgeFingerprintHelper
{
    /// <summary>
    /// 计算文件的 CurseForge Fingerprint
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>Fingerprint 值（uint）</returns>
    public static uint ComputeFingerprint(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        return ComputeFingerprint(stream);
    }

    /// <summary>
    /// 计算流的 CurseForge Fingerprint
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <returns>Fingerprint 值（uint）</returns>
    public static uint ComputeFingerprint(Stream stream)
    {
        // 读取文件内容并过滤空白字符
        var normalizedBytes = new List<byte>();
        
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            byte currentByte = (byte)b;
            
            // 跳过空白字符：9=Tab, 10=LF, 13=CR, 32=Space
            if (currentByte == 9 || currentByte == 10 || currentByte == 13 || currentByte == 32)
            {
                continue;
            }
            
            normalizedBytes.Add(currentByte);
        }
        
        // 使用 MurmurHash2 计算哈希值
        return ComputeMurmurHash2(normalizedBytes.ToArray());
    }

    /// <summary>
    /// MurmurHash2 算法实现
    /// </summary>
    /// <param name="data">输入数据</param>
    /// <param name="seed">种子值，CurseForge 使用 1</param>
    /// <returns>哈希值</returns>
    private static uint ComputeMurmurHash2(byte[] data, uint seed = 1)
    {
        const uint m = 0x5bd1e995;
        const int r = 24;

        int length = data.Length;
        if (length == 0)
        {
            return 0;
        }

        uint h = seed ^ (uint)length;
        int currentIndex = 0;

        // 处理 4 字节块
        while (length >= 4)
        {
            uint k = BitConverter.ToUInt32(data, currentIndex);
            
            k *= m;
            k ^= k >> r;
            k *= m;

            h *= m;
            h ^= k;

            currentIndex += 4;
            length -= 4;
        }

        // 处理剩余字节
        switch (length)
        {
            case 3:
                h ^= (uint)data[currentIndex + 2] << 16;
                goto case 2;
            case 2:
                h ^= (uint)data[currentIndex + 1] << 8;
                goto case 1;
            case 1:
                h ^= data[currentIndex];
                h *= m;
                break;
        }

        // 最终混合
        h ^= h >> 13;
        h *= m;
        h ^= h >> 15;

        return h;
    }

    /// <summary>
    /// 批量计算多个文件的 Fingerprint
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    /// <returns>文件路径到 Fingerprint 的映射</returns>
    public static Dictionary<string, uint> ComputeFingerprints(IEnumerable<string> filePaths)
    {
        var result = new Dictionary<string, uint>();
        
        foreach (var filePath in filePaths)
        {
            try
            {
                var fingerprint = ComputeFingerprint(filePath);
                result[filePath] = fingerprint;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算 Fingerprint 失败: {filePath}, 错误: {ex.Message}");
            }
        }
        
        return result;
    }
}
