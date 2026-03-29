using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public class AgentSettingsActionPreviewHelperTests
{
    [Fact]
    public void BuildPreviewMessage_ForGlobalScope_RendersFieldDiffs()
    {
        var message = AgentSettingsActionPreviewHelper.BuildPreviewMessage(new AgentSettingsActionPreviewInput
        {
            Scope = "global",
            Changes =
            [
                new AgentSettingsActionPreviewChange
                {
                    DisplayName = "Java 选择方式",
                    OldValue = "auto",
                    NewValue = "manual"
                },
                new AgentSettingsActionPreviewChange
                {
                    DisplayName = "JVM 参数",
                    OldValue = null,
                    NewValue = "-Dglobal=true"
                }
            ]
        });

        message.Should().Contain("已准备修改全局设置：");
        message.Should().Contain("- Java 选择方式: auto -> manual");
        message.Should().Contain("- JVM 参数: 未设置 -> -Dglobal=true");
    }

    [Fact]
    public void BuildPreviewMessage_ForInstanceScope_RendersTargetAndModeSwitches()
    {
        var message = AgentSettingsActionPreviewHelper.BuildPreviewMessage(new AgentSettingsActionPreviewInput
        {
            Scope = "instance",
            TargetName = "1.21.1-Fabric",
            Changes =
            [
                new AgentSettingsActionPreviewChange
                {
                    DisplayName = "Java 路径",
                    OldValue = "使用全局设置",
                    NewValue = @"C:\Java\jdk-21\bin\javaw.exe",
                    SwitchesToOverride = true
                },
                new AgentSettingsActionPreviewChange
                {
                    DisplayName = "内存设置",
                    OldValue = "版本独立",
                    NewValue = "跟随全局",
                    SwitchesToFollowGlobal = true
                }
            ]
        });

        message.Should().Contain("已准备修改实例“1.21.1-Fabric”的设置：");
        message.Should().Contain("- Java 路径: 使用全局设置 -> C:\\Java\\jdk-21\\bin\\javaw.exe");
        message.Should().Contain("这会自动切换为版本独立设置。");
        message.Should().Contain("这会自动切换为跟随全局设置。");
    }

    [Fact]
    public void BuildPreviewMessage_WithoutChanges_RendersFallbackLine()
    {
        var message = AgentSettingsActionPreviewHelper.BuildPreviewMessage(new AgentSettingsActionPreviewInput
        {
            Scope = "global",
            Changes = []
        });

        message.Should().Contain("当前没有可预览的字段变化。");
    }
}