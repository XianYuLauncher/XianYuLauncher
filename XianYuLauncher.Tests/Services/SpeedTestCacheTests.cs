using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Services;

public class SpeedTestCacheTests
{
    #region IsExpired 测试

    [Fact]
    public void IsExpired_NewCache_ReturnsTrue()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            LastUpdated = DateTime.UtcNow.AddHours(-13)
        };

        // Act
        var result = cache.IsExpired;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsExpired_FreshCache_ReturnsFalse()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            LastUpdated = DateTime.UtcNow.AddHours(-6)
        };

        // Act
        var result = cache.IsExpired;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsExpired_Exactly12Hours_ReturnsTrue()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            LastUpdated = DateTime.UtcNow.AddHours(-12)
        };

        // Act
        var result = cache.IsExpired;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsExpired_EmptyLastUpdated_ReturnsTrue()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            LastUpdated = default
        };

        // Act
        var result = cache.IsExpired;

        // Assert
        Assert.True(result);
    }

    #endregion

    #region GetFastestVersionManifestSourceKey 测试

    [Fact]
    public void GetFastestVersionManifestSourceKey_EmptyCache_ReturnsNull()
    {
        // Arrange
        var cache = new SpeedTestCache();

        // Act
        var result = cache.GetFastestVersionManifestSourceKey();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFastestVersionManifestSourceKey_ReturnsLowestLatency()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            VersionManifestSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 100, IsSuccess = true },
                ["mcim"] = new SpeedTestResult { SourceKey = "mcim", LatencyMs = 80, IsSuccess = true }
            }
        };

        // Act
        var result = cache.GetFastestVersionManifestSourceKey();

        // Assert
        Assert.Equal("bmclapi", result);
    }

    [Fact]
    public void GetFastestVersionManifestSourceKey_IgnoresFailedSources()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            VersionManifestSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 100, IsSuccess = true }
            }
        };

        // Act
        var result = cache.GetFastestVersionManifestSourceKey();

        // Assert
        Assert.Equal("official", result);
    }

    [Fact]
    public void GetFastestVersionManifestSourceKey_AllFailed_ReturnsNull()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            VersionManifestSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 100, IsSuccess = false }
            }
        };

        // Act
        var result = cache.GetFastestVersionManifestSourceKey();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetFastestFileDownloadSourceKey 测试

    [Fact]
    public void GetFastestFileDownloadSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestFileDownloadSourceKey());
    }

    [Fact]
    public void GetFastestFileDownloadSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            FileDownloadSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 40, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 120, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestFileDownloadSourceKey());
    }

    [Fact]
    public void GetFastestFileDownloadSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            FileDownloadSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 40, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 120, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestFileDownloadSourceKey());
    }

    [Fact]
    public void GetFastestFileDownloadSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            FileDownloadSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 40, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 120, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestFileDownloadSourceKey());
    }

    #endregion

    #region GetFastestForgeSourceKey 测试

    [Fact]
    public void GetFastestForgeSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestForgeSourceKey());
    }

    [Fact]
    public void GetFastestForgeSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            ForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 55, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestForgeSourceKey());
    }

    [Fact]
    public void GetFastestForgeSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            ForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 55, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestForgeSourceKey());
    }

    [Fact]
    public void GetFastestForgeSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            ForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 55, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestForgeSourceKey());
    }

    #endregion

    #region GetFastestFabricSourceKey 测试

    [Fact]
    public void GetFastestFabricSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestFabricSourceKey());
    }

    [Fact]
    public void GetFastestFabricSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            FabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 30, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 150, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestFabricSourceKey());
    }

    [Fact]
    public void GetFastestFabricSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            FabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 30, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 150, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestFabricSourceKey());
    }

    [Fact]
    public void GetFastestFabricSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            FabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 30, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 150, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestFabricSourceKey());
    }

    #endregion

    #region GetFastestNeoForgeSourceKey 测试

    [Fact]
    public void GetFastestNeoForgeSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestNeoForgeSourceKey());
    }

    [Fact]
    public void GetFastestNeoForgeSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            NeoForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 45, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 180, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestNeoForgeSourceKey());
    }

    [Fact]
    public void GetFastestNeoForgeSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            NeoForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 45, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 180, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestNeoForgeSourceKey());
    }

    [Fact]
    public void GetFastestNeoForgeSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            NeoForgeSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 45, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 180, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestNeoForgeSourceKey());
    }

    #endregion

    #region GetFastestLiteLoaderSourceKey 测试

    [Fact]
    public void GetFastestLiteLoaderSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestLiteLoaderSourceKey());
    }

    [Fact]
    public void GetFastestLiteLoaderSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            LiteLoaderSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 60, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 250, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestLiteLoaderSourceKey());
    }

    [Fact]
    public void GetFastestLiteLoaderSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            LiteLoaderSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 60, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 250, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestLiteLoaderSourceKey());
    }

    [Fact]
    public void GetFastestLiteLoaderSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            LiteLoaderSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 60, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 250, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestLiteLoaderSourceKey());
    }

    #endregion

    #region GetFastestQuiltSourceKey 测试

    [Fact]
    public void GetFastestQuiltSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestQuiltSourceKey());
    }

    [Fact]
    public void GetFastestQuiltSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            QuiltSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 35, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 130, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestQuiltSourceKey());
    }

    [Fact]
    public void GetFastestQuiltSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            QuiltSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 35, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 130, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestQuiltSourceKey());
    }

    [Fact]
    public void GetFastestQuiltSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            QuiltSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 35, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 130, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestQuiltSourceKey());
    }

    #endregion

    #region GetFastestLegacyFabricSourceKey 测试

    [Fact]
    public void GetFastestLegacyFabricSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestLegacyFabricSourceKey());
    }

    [Fact]
    public void GetFastestLegacyFabricSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            LegacyFabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 70, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 300, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestLegacyFabricSourceKey());
    }

    [Fact]
    public void GetFastestLegacyFabricSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            LegacyFabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 70, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 300, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestLegacyFabricSourceKey());
    }

    [Fact]
    public void GetFastestLegacyFabricSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            LegacyFabricSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 70, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 300, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestLegacyFabricSourceKey());
    }

    #endregion

    #region GetFastestCleanroomSourceKey 测试

    [Fact]
    public void GetFastestCleanroomSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestCleanroomSourceKey());
    }

    [Fact]
    public void GetFastestCleanroomSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            CleanroomSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestCleanroomSourceKey());
    }

    [Fact]
    public void GetFastestCleanroomSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            CleanroomSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestCleanroomSourceKey());
    }

    [Fact]
    public void GetFastestCleanroomSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            CleanroomSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 50, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 200, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestCleanroomSourceKey());
    }

    #endregion

    #region GetFastestOptifineSourceKey 测试

    [Fact]
    public void GetFastestOptifineSourceKey_EmptyCache_ReturnsNull()
    {
        var cache = new SpeedTestCache();
        Assert.Null(cache.GetFastestOptifineSourceKey());
    }

    [Fact]
    public void GetFastestOptifineSourceKey_ReturnsLowestLatency()
    {
        var cache = new SpeedTestCache
        {
            OptifineSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 65, IsSuccess = true },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 220, IsSuccess = true }
            }
        };
        Assert.Equal("bmclapi", cache.GetFastestOptifineSourceKey());
    }

    [Fact]
    public void GetFastestOptifineSourceKey_IgnoresFailedSources()
    {
        var cache = new SpeedTestCache
        {
            OptifineSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 65, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 220, IsSuccess = true }
            }
        };
        Assert.Equal("official", cache.GetFastestOptifineSourceKey());
    }

    [Fact]
    public void GetFastestOptifineSourceKey_AllFailed_ReturnsNull()
    {
        var cache = new SpeedTestCache
        {
            OptifineSources = new Dictionary<string, SpeedTestResult>
            {
                ["bmclapi"] = new SpeedTestResult { SourceKey = "bmclapi", LatencyMs = 65, IsSuccess = false },
                ["official"] = new SpeedTestResult { SourceKey = "official", LatencyMs = 220, IsSuccess = false }
            }
        };
        Assert.Null(cache.GetFastestOptifineSourceKey());
    }

    #endregion

    #region GetFastestCommunitySourceKey 测试

    [Fact]
    public void GetFastestCommunitySourceKey_EmptyCache_ReturnsNull()
    {
        // Arrange
        var cache = new SpeedTestCache();

        // Act
        var result = cache.GetFastestCommunitySourceKey();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFastestCommunitySourceKey_ReturnsLowestLatency()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            CommunitySources = new Dictionary<string, SpeedTestResult>
            {
                ["mcim"] = new SpeedTestResult { SourceKey = "mcim", LatencyMs = 60, IsSuccess = true },
                ["modrinth"] = new SpeedTestResult { SourceKey = "modrinth", LatencyMs = 150, IsSuccess = true },
                ["custom"] = new SpeedTestResult { SourceKey = "custom", LatencyMs = 90, IsSuccess = true }
            }
        };

        // Act
        var result = cache.GetFastestCommunitySourceKey();

        // Assert
        Assert.Equal("mcim", result);
    }

    [Fact]
    public void GetFastestCommunitySourceKey_IgnoresFailedSources()
    {
        // Arrange
        var cache = new SpeedTestCache
        {
            CommunitySources = new Dictionary<string, SpeedTestResult>
            {
                ["mcim"] = new SpeedTestResult { SourceKey = "mcim", LatencyMs = 60, IsSuccess = false },
                ["modrinth"] = new SpeedTestResult { SourceKey = "modrinth", LatencyMs = 150, IsSuccess = true }
            }
        };

        // Act
        var result = cache.GetFastestCommunitySourceKey();

        // Assert
        Assert.Equal("modrinth", result);
    }

    #endregion

    #region SpeedTestResult 测试

    [Fact]
    public void SpeedTestResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new SpeedTestResult();

        // Assert
        Assert.Equal(string.Empty, result.SourceKey);
        Assert.Equal(string.Empty, result.SourceName);
        Assert.Equal(0, result.LatencyMs);
        Assert.Equal(0, result.SpeedKBps);
        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SpeedTestResult_SetProperties_WorksCorrectly()
    {
        // Arrange & Act
        var result = new SpeedTestResult
        {
            SourceKey = "bmclapi",
            SourceName = "BMCLAPI",
            LatencyMs = 50,
            SpeedKBps = 500,
            IsSuccess = true,
            ErrorMessage = null,
            Timestamp = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("bmclapi", result.SourceKey);
        Assert.Equal("BMCLAPI", result.SourceName);
        Assert.Equal(50, result.LatencyMs);
        Assert.Equal(500, result.SpeedKBps);
        Assert.True(result.IsSuccess);
    }

    #endregion
}
