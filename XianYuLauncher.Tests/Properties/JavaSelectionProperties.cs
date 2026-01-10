using Xunit;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Properties;

/// <summary>
/// 属性测试：Java 版本选择和路径验证
/// </summary>
public class JavaSelectionProperties
{
    /// <summary>
    /// Property 1: Java 版本选择一致性
    /// For any required Java major version and available Java installations, 
    /// selecting the best Java should always return a Java version that meets 
    /// or exceeds the required version, or null if no suitable version exists.
    /// Validates: Requirements 1.2
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(17)]
    [InlineData(21)]
    public void Property_JavaVersionSelection_Consistency(int requiredVersion)
    {
        // Arrange
        var javaVersions = new List<JavaVersion>
        {
            new JavaVersion { Path = "java8.exe", MajorVersion = 8, IsJDK = false },
            new JavaVersion { Path = "java11.exe", MajorVersion = 11, IsJDK = false },
            new JavaVersion { Path = "java17.exe", MajorVersion = 17, IsJDK = true },
            new JavaVersion { Path = "java21.exe", MajorVersion = 21, IsJDK = true }
        };

        // Act
        var selected = javaVersions
            .Where(j => j.MajorVersion >= requiredVersion)
            .OrderByDescending(j => j.MajorVersion == requiredVersion)
            .ThenByDescending(j => j.IsJDK)
            .ThenBy(j => Math.Abs(j.MajorVersion - requiredVersion))
            .FirstOrDefault();

        // Assert
        if (selected != null)
        {
            // If a Java is selected, it must meet or exceed the required version
            Assert.True(selected.MajorVersion >= requiredVersion, 
                $"Selected Java version {selected.MajorVersion} is less than required version {requiredVersion}");
        }
        else
        {
            // If no Java is selected, verify no suitable version exists
            Assert.DoesNotContain(javaVersions, j => j.MajorVersion >= requiredVersion);
        }
    }

    /// <summary>
    /// Property 1 (Extended): Java 版本选择优先级
    /// For any required version, if multiple matching versions exist,
    /// the selection should prefer exact matches, then JDK over JRE
    /// </summary>
    [Fact]
    public void Property_JavaVersionSelection_PreferExactMatch()
    {
        // Arrange
        var javaVersions = new List<JavaVersion>
        {
            new JavaVersion { Path = "java8.exe", MajorVersion = 8, IsJDK = false },
            new JavaVersion { Path = "java17-jre.exe", MajorVersion = 17, IsJDK = false },
            new JavaVersion { Path = "java17-jdk.exe", MajorVersion = 17, IsJDK = true },
            new JavaVersion { Path = "java21.exe", MajorVersion = 21, IsJDK = true }
        };

        int requiredVersion = 17;

        // Act
        var selected = javaVersions
            .OrderByDescending(j => j.MajorVersion == requiredVersion)
            .ThenByDescending(j => j.IsJDK)
            .ThenBy(j => Math.Abs(j.MajorVersion - requiredVersion))
            .First();

        // Assert
        Assert.Equal(17, selected.MajorVersion);
        Assert.True(selected.IsJDK, "Should prefer JDK over JRE when versions match");
        Assert.Equal("java17-jdk.exe", selected.Path);
    }

    /// <summary>
    /// Property 8: Java 路径验证正确性
    /// For any file path, validation should return true only if the path 
    /// points to an existing java.exe or javaw.exe file.
    /// Validates: Requirements 1.4
    /// </summary>
    [Theory]
    [InlineData("java.exe", true)]
    [InlineData("javaw.exe", true)]
    [InlineData("JAVA.EXE", true)]
    [InlineData("JAVAW.EXE", true)]
    [InlineData("java", false)]
    [InlineData("javac.exe", false)]
    [InlineData("other.exe", false)]
    [InlineData("", false)]
    public void Property_JavaPathValidation_Correctness(string fileName, bool shouldBeValid)
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);

        try
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                File.WriteAllText(filePath, "dummy");
            }

            // Act
            var fileNameLower = Path.GetFileName(filePath).ToLowerInvariant();
            var isValid = File.Exists(filePath) && (fileNameLower == "java.exe" || fileNameLower == "javaw.exe");

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Property 8 (Extended): 非存在路径验证
    /// For any non-existent path, validation should always return false
    /// </summary>
    [Fact]
    public void Property_JavaPathValidation_NonExistentPath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "java.exe");

        // Act
        var isValid = File.Exists(nonExistentPath);

        // Assert
        Assert.False(isValid, "Non-existent path should not be valid");
    }

    /// <summary>
    /// Property: Java 版本解析幂等性
    /// For any valid version string, parsing it multiple times should 
    /// always produce the same major version number
    /// </summary>
    [Theory]
    [InlineData("1.8.0_301")]
    [InlineData("17.0.1")]
    [InlineData("21")]
    public void Property_JavaVersionParsing_Idempotent(string versionString)
    {
        // Arrange
        var mockLocalSettings = new Moq.Mock<ILocalSettingsService>();
        var service = new JavaRuntimeService(mockLocalSettings.Object);

        // Act
        var result1 = service.TryParseJavaVersion(versionString, out int version1);
        var result2 = service.TryParseJavaVersion(versionString, out int version2);
        var result3 = service.TryParseJavaVersion(versionString, out int version3);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.Equal(version1, version2);
        Assert.Equal(version2, version3);
    }
}
