using System.Security.Cryptography;
using System.Text;

namespace XianYuLauncher.Helpers;

public static class HashHelper
{
    public static string ComputeMD5(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}