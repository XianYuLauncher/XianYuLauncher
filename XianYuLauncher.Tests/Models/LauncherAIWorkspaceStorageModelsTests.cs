using FluentAssertions;
using Newtonsoft.Json;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Models;

public sealed class LauncherAIWorkspaceStorageModelsTests
{
    [Fact]
    public void LauncherAIChatMessageStorageModel_ShouldRoundTripAiHistoryReasoningContent()
    {
        var conversation = new LauncherAIConversationStorageModel
        {
            ConversationId = Guid.NewGuid(),
            Session = new LauncherAISessionStorageModel
            {
                ChatMessages =
                [
                    new LauncherAIChatMessageStorageModel
                    {
                        Role = "assistant",
                        Content = "已准备提案",
                        AIHistoryContent = "已准备提案",
                        AIHistoryReasoningContent = "先读取当前配置，再决定调用工具。",
                        ToolCalls =
                        [
                            new ToolCallInfo
                            {
                                Id = "call_1",
                                FunctionName = "patchGlobalLaunchSettings",
                                Arguments = "{}"
                            }
                        ]
                    }
                ]
            }
        };

        var json = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<LauncherAIConversationStorageModel>(json);

        restored.Should().NotBeNull();
        restored!.Session.ChatMessages.Should().ContainSingle();
        restored.Session.ChatMessages[0].AIHistoryReasoningContent.Should().Be("先读取当前配置，再决定调用工具。");
    }
}
