using System.Runtime.InteropServices;
using System.Text;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Helpers;

public class RuntimeHelper
{
    public static bool IsMSIX
    {
        get => AppEnvironment.IsMSIX;
    }
}
