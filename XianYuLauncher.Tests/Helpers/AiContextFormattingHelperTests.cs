using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Helpers;

public sealed class AiContextFormattingHelperTests
{
    [Fact]
    public void RemoveClassPathArguments_ShouldOmitSeparatedClassPathTokens()
    {
        var command = "\"C:\\Program Files\\Java\\bin\\javaw.exe\" -Xmx4G -cp \"a;b;c\" net.minecraft.client.main.Main --username Steve";

        var result = AiContextFormattingHelper.RemoveClassPathArguments(command, out var classPathRemoved);

        classPathRemoved.Should().BeTrue();
        result.Should().Contain("\"C:\\Program Files\\Java\\bin\\javaw.exe\"");
        result.Should().Contain("-Xmx4G");
        result.Should().Contain("net.minecraft.client.main.Main");
        result.Should().Contain("--username Steve");
        result.Should().NotContain("-cp");
        result.Should().NotContain("a;b;c");
    }

    [Fact]
    public void RemoveClassPathArguments_ShouldOmitInlineClasspathTokens()
    {
        var command = "javaw.exe -classpath=a;b;c net.minecraft.client.main.Main";

        var result = AiContextFormattingHelper.RemoveClassPathArguments(command, out var classPathRemoved);

        classPathRemoved.Should().BeTrue();
        result.Should().Be("javaw.exe net.minecraft.client.main.Main");
    }

    [Fact]
    public void TryGetJavaExecutable_ShouldReturnFirstToken()
    {
        var command = "\"C:\\Program Files\\Java\\bin\\javaw.exe\" -Xmx4G -cp \"a;b;c\" net.minecraft.client.main.Main";

        var javaExecutable = AiContextFormattingHelper.TryGetJavaExecutable(command);

        javaExecutable.Should().Be(@"C:\Program Files\Java\bin\javaw.exe");
    }

    [Fact]
    public void GetTailSlice_ShouldReturnTailOffsets()
    {
        var slice = AiContextFormattingHelper.GetTailSlice("abcdef", 4);

        slice.Content.Should().Be("cdef");
        slice.StartOffset.Should().Be(2);
        slice.EndOffset.Should().Be(6);
        slice.TotalLength.Should().Be(6);
        slice.WasTruncated.Should().BeTrue();
    }

    [Fact]
    public void GetSlice_ShouldClampToAvailableRange()
    {
        var slice = AiContextFormattingHelper.GetSlice("abcdef", 4, 10);

        slice.Content.Should().Be("ef");
        slice.StartOffset.Should().Be(4);
        slice.EndOffset.Should().Be(6);
        slice.TotalLength.Should().Be(6);
        slice.WasTruncated.Should().BeTrue();
    }
}