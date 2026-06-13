using System;
using System.Collections.Generic;
using System.Linq;
using XianYuLauncher.Core.Contracts.Services;

namespace XianYuLauncher.Core.Services.ModLoaderInstallers;

/// <summary>
/// ModLoader 安装器工厂实现
/// </summary>
public class ModLoaderInstallerFactory : IModLoaderInstallerFactory
{
    private readonly Dictionary<string, IModLoaderInstaller> _installers;

    public ModLoaderInstallerFactory(IEnumerable<IModLoaderInstaller> installers)
    {
        _installers = installers.ToDictionary(
            i => i.ModLoaderType.ToLowerInvariant(),
            i => i,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public IModLoaderInstaller GetInstaller(string modLoaderType)
    {
        if (string.IsNullOrEmpty(modLoaderType))
        {
            throw new ArgumentException("ModLoader 类型不能为空", nameof(modLoaderType));
        }

        if (_installers.TryGetValue(modLoaderType.ToLowerInvariant(), out var installer))
        {
            return installer;
        }

        throw new NotSupportedException($"不支持的 ModLoader 类型: {modLoaderType}");
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetSupportedModLoaderTypes()
    {
        return _installers.Values.Select(i => i.ModLoaderType);
    }
}
