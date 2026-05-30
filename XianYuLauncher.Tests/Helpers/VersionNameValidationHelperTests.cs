using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class VersionNameValidationHelperTests
{
    [Theory]
    [InlineData("1.20.1-Fabric-0.15.0")]
    [InlineData("My Favorite Pack")]
    [InlineData("release-2024")]
    public void ValidateVersionName_ShouldAcceptTypicalNames(string versionName)
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName(versionName, minecraftPath);

            result.IsValid.Should().BeTrue();
            result.NormalizedName.Should().Be(versionName);
            result.Error.Should().Be(VersionNameValidationError.None);
            result.MaxSafeLength.Should().BeGreaterThan(0);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ValidateVersionName_ShouldRejectEmptyOrWhitespace(string versionName)
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName(versionName, minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.Empty);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("COM9")]
    [InlineData("lpt1")]
    [InlineData("NUL")]
    [InlineData("CONIN$")]
    public void ValidateVersionName_ShouldRejectWindowsReservedDeviceNames(string versionName)
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName(versionName, minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.ReservedDeviceName);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void ValidateVersionName_ShouldRejectReservedStemBeforeExtension()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName("aux.backup", minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.ReservedDeviceName);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void ValidateVersionName_ShouldAllowReservedWordAsNonStemSegment()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName("MyMod.con", minecraftPath);

            result.IsValid.Should().BeTrue();
            result.NormalizedName.Should().Be("MyMod.con");
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Theory]
    [InlineData("1.20.1 ")]
    [InlineData("1.20.1.")]
    public void ValidateVersionName_ShouldRejectTrailingSpaceOrDot(string versionName)
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName(versionName, minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.TrailingSpaceOrDot);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void ValidateVersionName_ShouldRejectInvalidFileNameCharacters()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName("bad|name", minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.InvalidChars);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void ValidateVersionName_ShouldRejectNamesExceedingMaxSafeLength()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var probe = VersionNameValidationHelper.ValidateVersionName("probe", minecraftPath);
            string tooLongName = new('a', probe.MaxSafeLength + 1);

            var result = VersionNameValidationHelper.ValidateVersionName(tooLongName, minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(VersionNameValidationError.TooLong);
            result.MaxSafeLength.Should().Be(probe.MaxSafeLength);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void ValidateVersionName_ShouldTrimLeadingWhitespaceForValidNames()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = VersionNameValidationHelper.ValidateVersionName("  1.20.1", minecraftPath);

            result.IsValid.Should().BeTrue();
            result.NormalizedName.Should().Be("1.20.1");
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    private static string CreateMinecraftRoot()
    {
        string minecraftPath = Path.Combine(Path.GetTempPath(), $"XianYuLauncher.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(minecraftPath, MinecraftPathConsts.Versions));
        return minecraftPath;
    }

    private static void DeleteMinecraftRoot(string minecraftPath)
    {
        if (Directory.Exists(minecraftPath))
        {
            Directory.Delete(minecraftPath, recursive: true);
        }
    }
}