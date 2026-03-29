using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class LauncherAIWorkspacePersistenceServiceTests : IDisposable
{
    private readonly string _workspaceRootPath;
    private readonly LauncherAIWorkspacePersistenceService _service;

    public LauncherAIWorkspacePersistenceServiceTests()
    {
        _workspaceRootPath = Path.Combine(Path.GetTempPath(), "launcher-ai-persistence", Guid.NewGuid().ToString("N"));
        _service = new LauncherAIWorkspacePersistenceService(
            new FileService(),
            new Mock<ILogger<LauncherAIWorkspacePersistenceService>>().Object,
            _workspaceRootPath);
    }

    [Fact]
    public async Task SaveWorkspaceAsync_ThenLoadWorkspaceAsync_ShouldRoundTrip()
    {
        var conversationId = Guid.NewGuid();
        var workspace = new LauncherAIWorkspaceStorageModel
        {
            SelectedConversationId = conversationId,
            ActiveErrorAnalysisConversationId = conversationId,
            NextConversationNumber = 4,
            Conversations =
            [
                new LauncherAIConversationIndexEntryStorageModel
                {
                    ConversationId = conversationId,
                    IsErrorAnalysisConversation = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                    LastUpdatedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };

        await _service.SaveWorkspaceAsync(workspace);

        var loaded = await _service.LoadWorkspaceAsync();

        loaded.Should().NotBeNull();
        loaded!.SelectedConversationId.Should().Be(conversationId);
        loaded.ActiveErrorAnalysisConversationId.Should().Be(conversationId);
        loaded.NextConversationNumber.Should().Be(4);
        loaded.Conversations.Should().ContainSingle();
        loaded.Conversations[0].ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public async Task SaveConversationAsync_ThenLoadConversationAsync_ShouldRoundTrip()
    {
        var conversationId = Guid.NewGuid();
        var conversation = new LauncherAIConversationStorageModel
        {
            ConversationId = conversationId,
            IsErrorAnalysisConversation = false,
            Title = "测试对话",
            ToolTip = "测试对话",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Interruption = new LauncherAIConversationInterruptionStorageModel
            {
                Kind = "tool_continuation_pending",
                InterruptedAtUtc = DateTimeOffset.UtcNow,
                Message = "已中断"
            },
            Session = new LauncherAISessionStorageModel
            {
                ChatInput = "草稿",
                IsChatEnabled = true,
                ChatMessages =
                [
                    new LauncherAIChatMessageStorageModel
                    {
                        Role = "user",
                        Content = "你好",
                        DisplayRoleText = "user",
                        ImageAttachments = []
                    }
                ],
                ActionProposals =
                [
                    new LauncherAIActionProposalStorageModel
                    {
                        ActionType = "launchGame",
                        ButtonText = "启动游戏",
                        DisplayMessage = "确认启动",
                        PermissionLevel = "ConfirmationRequired",
                        Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["path"] = @"D:\\.minecraft\\versions\\1.20.1"
                        }
                    }
                ]
            }
        };

        await _service.SaveConversationAsync(conversation);

        var loaded = await _service.LoadConversationAsync(conversationId);

        loaded.Should().NotBeNull();
        loaded!.ConversationId.Should().Be(conversationId);
        loaded.Title.Should().Be("测试对话");
        loaded.Interruption.Should().NotBeNull();
        loaded.Session.ChatInput.Should().Be("草稿");
        loaded.Session.ChatMessages.Should().ContainSingle();
        loaded.Session.ActionProposals.Should().ContainSingle();
        loaded.Session.ActionProposals[0].ActionType.Should().Be("launchGame");
    }

    [Fact]
    public async Task PersistAttachmentAsync_ShouldCopyFileIntoConversationAttachmentFolder()
    {
        var conversationId = Guid.NewGuid();
        var sourcePath = CreateTempImageFile();
        var attachment = new ChatImageAttachment
        {
            FileName = Path.GetFileName(sourcePath),
            FilePath = sourcePath,
            ContentType = "image/png"
        };

        var stored = await _service.PersistAttachmentAsync(conversationId, attachment);

        stored.Should().NotBeNull();
        stored!.RelativeFilePath.Should().StartWith($"{conversationId:N}/");

        var restored = _service.RestoreAttachment(stored);
        restored.Should().NotBeNull();
        restored!.FilePath.Should().NotBe(sourcePath);
        File.Exists(restored.FilePath).Should().BeTrue();
        restored.DataUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task PersistAttachmentAsync_WhenAttachmentAlreadyStored_ShouldReuseStoredRelativePath()
    {
        var conversationId = Guid.NewGuid();
        var sourcePath = CreateTempImageFile();
        var firstStored = await _service.PersistAttachmentAsync(
            conversationId,
            new ChatImageAttachment
            {
                FileName = Path.GetFileName(sourcePath),
                FilePath = sourcePath,
                ContentType = "image/png"
            });
        var restored = _service.RestoreAttachment(firstStored!);

        var secondStored = await _service.PersistAttachmentAsync(conversationId, restored!);

        secondStored.Should().NotBeNull();
        secondStored!.RelativeFilePath.Should().Be(firstStored!.RelativeFilePath);
    }

    [Fact]
    public async Task DeleteConversationAsync_ShouldRemoveConversationFileAndAttachmentFolder()
    {
        var conversationId = Guid.NewGuid();
        var conversation = new LauncherAIConversationStorageModel
        {
            ConversationId = conversationId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Session = new LauncherAISessionStorageModel()
        };
        await _service.SaveConversationAsync(conversation);

        var sourcePath = CreateTempImageFile();
        var stored = await _service.PersistAttachmentAsync(
            conversationId,
            new ChatImageAttachment
            {
                FileName = Path.GetFileName(sourcePath),
                FilePath = sourcePath,
                ContentType = "image/png"
            });
        var restored = _service.RestoreAttachment(stored!);

        await _service.DeleteConversationAsync(conversationId);

        var loaded = await _service.LoadConversationAsync(conversationId);
        loaded.Should().BeNull();
        restored.Should().NotBeNull();
        File.Exists(restored!.FilePath).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRootPath))
        {
            Directory.Delete(_workspaceRootPath, recursive: true);
        }
    }

    private static string CreateTempImageFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "launcher-ai-persistence-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "sample.png");
        File.WriteAllBytes(filePath, [137, 80, 78, 71, 13, 10, 26, 10]);
        return filePath;
    }
}