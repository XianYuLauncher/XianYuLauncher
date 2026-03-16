using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;
using XianYuLauncher.Core.Services.DownloadSource;
using XianYuLauncher.Core.Services.ModLoaderInstallers;

namespace XianYuLauncher.Tests.Services.ModLoaderInstallers;

public class CleanroomInstallerTests
{
    [Fact]
    public void ResolveVersionInfo_PrefersCleanroomLibrariesWithoutDroppingJna()
    {
        var installer = new CleanroomInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            Mock.Of<IProcessorExecutor>(),
            Mock.Of<IJavaRuntimeService>(),
            new DownloadSourceFactory(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<CleanroomInstaller>>());

        var original = new VersionInfo
        {
            Id = "1.12.2",
            AssetIndex = new AssetIndex { Id = "1.12" },
            Libraries = new List<Library>
            {
                new() { Name = "net.java.dev.jna:jna:4.4.0" },
                new() { Name = "org.ow2.asm:asm:5.0.3" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var cleanroom = new VersionInfo
        {
            Id = "cleanroom-0.4.2-alpha",
            Libraries = new List<Library>
            {
                new() { Name = "com.cleanroommc:cleanroom:0.4.2-alpha" },
                new() { Name = "net.java.dev.jna:jna:5.13.0" },
                new() { Name = "org.ow2.asm:asm:9.7" }
            }
        };

        var method = typeof(CleanroomInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(installer, new object[] { original, cleanroom, new List<Library>() }));

        Assert.Collection(
            merged.Libraries!,
            library => Assert.Equal("com.cleanroommc:cleanroom:0.4.2-alpha", library.Name),
            library => Assert.Equal("net.java.dev.jna:jna:5.13.0", library.Name),
            library => Assert.Equal("org.ow2.asm:asm:9.7", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
        Assert.Equal("1.12", merged.Assets);
        Assert.Equal(21, merged.JavaVersion!.MajorVersion);
        Assert.Equal(
            "https://repo.cleanroommc.com/releases/com/cleanroommc/cleanroom/0.4.2-alpha/cleanroom-0.4.2-alpha.jar",
            merged.Libraries![0].Downloads!.Artifact!.Url);
    }

    [Fact]
    public void ResolveVersionInfo_DoesNotIncludeInstallProfileLibrariesInFinalManifest()
    {
        var installer = new CleanroomInstaller(
            Mock.Of<IDownloadManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IVersionInfoManager>(),
            Mock.Of<IProcessorExecutor>(),
            Mock.Of<IJavaRuntimeService>(),
            new DownloadSourceFactory(),
            new UnifiedVersionManifestResolver(),
            Mock.Of<ILogger<CleanroomInstaller>>());

        var original = new VersionInfo
        {
            Id = "1.12.2",
            AssetIndex = new AssetIndex { Id = "1.12" },
            Libraries = new List<Library>
            {
                new() { Name = "com.google.guava:guava:21.0" },
                new() { Name = "com.mojang:brigadier:1.0.18" }
            }
        };
        var cleanroom = new VersionInfo
        {
            Id = "cleanroom-0.4.2-alpha",
            Libraries = new List<Library>
            {
                new() { Name = "com.cleanroommc:cleanroom:0.4.2-alpha" }
            }
        };
        var additionalLibraries = new List<Library>
        {
            new() { Name = "com.google.guava:guava:32.1.2-jre" }
        };

        var method = typeof(CleanroomInstaller).GetMethod("ResolveVersionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var merged = Assert.IsType<VersionInfo>(method!.Invoke(installer, new object[] { original, cleanroom, additionalLibraries }));

        Assert.Collection(
            merged.Libraries!,
            library => Assert.Equal("com.cleanroommc:cleanroom:0.4.2-alpha", library.Name),
            library => Assert.Equal("com.google.guava:guava:21.0", library.Name),
            library => Assert.Equal("com.mojang:brigadier:1.0.18", library.Name));
        Assert.Equal("1.12", merged.Assets);
        Assert.DoesNotContain(merged.Libraries!, library => library.Name == "com.google.guava:guava:32.1.2-jre");
    }
}