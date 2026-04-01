using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class ModpackInstallNameValidationHelperTests
{
    [Fact]
    public void Validate_ShouldPreserveSpacesInName()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            var result = ModpackInstallNameValidationHelper.Validate("My Favorite Pack", minecraftPath);

            result.IsValid.Should().BeTrue();
            result.NormalizedName.Should().Be("My Favorite Pack");
            result.Error.Should().Be(ModpackInstallNameValidationError.None);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void Validate_ShouldReturnAlreadyExists_WhenVersionDirectoryExists()
    {
        string minecraftPath = CreateMinecraftRoot();
        Directory.CreateDirectory(Path.Combine(minecraftPath, MinecraftPathConsts.Versions, "Existing Pack"));

        try
        {
            var result = ModpackInstallNameValidationHelper.Validate("Existing Pack", minecraftPath);

            result.IsValid.Should().BeFalse();
            result.NormalizedName.Should().Be("Existing Pack");
            result.Error.Should().Be(ModpackInstallNameValidationError.AlreadyExists);
        }
        finally
        {
            DeleteMinecraftRoot(minecraftPath);
        }
    }

    [Fact]
    public void Validate_ShouldSurfaceTooLongError()
    {
        string minecraftPath = CreateMinecraftRoot();

        try
        {
            string tooLongName = new('a', 300);
            var result = ModpackInstallNameValidationHelper.Validate(tooLongName, minecraftPath);

            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(ModpackInstallNameValidationError.TooLong);
            result.MaxSafeLength.Should().BeGreaterThan(0);
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