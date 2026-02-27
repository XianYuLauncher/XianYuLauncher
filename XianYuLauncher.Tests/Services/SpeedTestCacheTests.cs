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
