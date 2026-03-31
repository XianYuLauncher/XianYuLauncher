using FluentAssertions;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Tests.Models;

public sealed class ChatMessageTests
{
    [Fact]
    public void Constructor_ShouldInitializeImageAttachmentsToEmptyList()
    {
        var message = new ChatMessage("user", "hello");

        message.ImageAttachments.Should().NotBeNull();
        message.ImageAttachments.Should().BeEmpty();
    }

    [Fact]
    public void ImageAttachments_WhenAssignedNull_ShouldCoalesceToEmptyList()
    {
        var message = new ChatMessage("user", "hello")
        {
            ImageAttachments = null!
        };

        message.ImageAttachments.Should().NotBeNull();
        message.ImageAttachments.Should().BeEmpty();
    }
}