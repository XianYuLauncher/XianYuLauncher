using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class OpenAiAnalysisServiceTests
{
    [Fact]
    public async Task StreamChatWithToolsAsync_ShouldIgnoreTailContentAfterToolCallStarts()
    {
        var payloads = new[]
        {
            "{\"choices\":[{\"delta\":{\"content\":\"已为你准备删除。\"}}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"function\":{\"name\":\"deleteMod\",\"arguments\":\"{\\\"modId\\\":\\\"Test.jar\"}}]}}]}",
            "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"}\"}}]}}]}",
            "{\"choices\":[{\"delta\":{\"content\":\"已拒绝执行：删除 Mod\"}}]}"
        };

        await using var server = await FakeSseServer.StartAsync(payloads);
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var chunks = await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "请删除 Test.jar")],
            [],
            "test-key",
            server.Endpoint,
            "test-model"));

        var content = string.Concat(chunks.Where(chunk => chunk.IsContent).Select(chunk => chunk.ContentDelta));
        content.Should().Be("已为你准备删除。");

        var toolCallChunk = chunks.Should().ContainSingle(chunk => chunk.IsToolCall).Subject;
        toolCallChunk.ToolCalls.Should().ContainSingle();
        toolCallChunk.ToolCalls![0].FunctionName.Should().Be("deleteMod");
        toolCallChunk.ToolCalls[0].Arguments.Should().Be("{\"modId\":\"Test.jar\"}");
    }

    [Fact]
    public async Task StreamChatWithToolsAsync_WithoutToolCalls_ShouldKeepAllContent()
    {
        var payloads = new[]
        {
            "{\"choices\":[{\"delta\":{\"content\":\"第一段\"}}]}",
            "{\"choices\":[{\"delta\":{\"content\":\"第二段\"}}]}"
        };

        await using var server = await FakeSseServer.StartAsync(payloads);
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var chunks = await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "你好")],
            [],
            "test-key",
            server.Endpoint,
            "test-model"));

        var content = string.Concat(chunks.Where(chunk => chunk.IsContent).Select(chunk => chunk.ContentDelta));
        content.Should().Be("第一段第二段");
        chunks.Should().NotContain(chunk => chunk.IsToolCall);
    }

    private static async Task<List<AiStreamChunk>> CollectChunksAsync(IAsyncEnumerable<AiStreamChunk> source)
    {
        List<AiStreamChunk> chunks = [];
        await foreach (var chunk in source)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private sealed class FakeSseServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _responseTask;

        private FakeSseServer(HttpListener listener, Task responseTask, string endpoint)
        {
            _listener = listener;
            _responseTask = responseTask;
            Endpoint = endpoint;
        }

        public string Endpoint { get; }

        public static async Task<FakeSseServer> StartAsync(IReadOnlyList<string> payloads)
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/v1/chat/completions/";
            var endpoint = prefix.TrimEnd('/');
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var responseTask = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "text/event-stream";
                context.Response.SendChunked = true;

                await using var writer = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false), leaveOpen: false);
                foreach (var payload in payloads)
                {
                    await writer.WriteAsync($"data: {payload}\n\n");
                    await writer.FlushAsync();
                }

                await writer.WriteAsync("data: [DONE]\n\n");
                await writer.FlushAsync();
                context.Response.Close();
            });

            await Task.Yield();
            return new FakeSseServer(listener, responseTask, endpoint);
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _responseTask;
            }
            catch (HttpListenerException)
            {
            }
            finally
            {
                _listener.Close();
            }
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}