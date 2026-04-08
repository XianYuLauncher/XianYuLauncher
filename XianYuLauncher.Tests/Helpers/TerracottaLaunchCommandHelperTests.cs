using System;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class TerracottaLaunchCommandHelperTests
{
    [Fact]
    public void BuildHmclStartupCommandArguments_ShouldOnlyIncludeHmclBootstrapArguments()
    {
        var arguments = TerracottaLaunchCommandHelper.BuildHmclStartupCommandArguments(
            @"C:\Users\pc\AppData\Local\Packages\SpiritStudio.XianYuLauncher\LocalState\terracotta",
            "terracotta-0.4.2-windows-x86_64.exe",
            @"C:\Users\pc\AppData\Local\Packages\SpiritStudio.XianYuLauncher\LocalState\temp\terracotta-Port.json");

        arguments.Should().Be("/c cd /d \"C:\\Users\\pc\\AppData\\Local\\Packages\\SpiritStudio.XianYuLauncher\\LocalState\\terracotta\" && \"terracotta-0.4.2-windows-x86_64.exe\" --hmcl \"C:\\Users\\pc\\AppData\\Local\\Packages\\SpiritStudio.XianYuLauncher\\LocalState\\temp\\terracotta-Port.json\"");
        arguments.Should().NotContain("--client");
        arguments.Should().NotContain("--id");
    }

    [Theory]
    [InlineData("", "terracotta.exe", @"C:\temp\terracotta-Port.json")]
    [InlineData(@"C:\terracotta", "", @"C:\temp\terracotta-Port.json")]
    [InlineData(@"C:\terracotta", "terracotta.exe", "")]
    public void BuildHmclStartupCommandArguments_WhenRequiredInputMissing_ShouldThrow(
        string workingDirectory,
        string executableName,
        string hmclFilePath)
    {
        Action action = () => TerracottaLaunchCommandHelper.BuildHmclStartupCommandArguments(
            workingDirectory,
            executableName,
            hmclFilePath);

        action.Should().Throw<ArgumentException>();
    }
}