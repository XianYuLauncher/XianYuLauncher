using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Exceptions;
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

    [Fact]
    public async Task StreamChatWithToolsAsync_WithImageAttachments_ShouldSerializeImageUrlParts()
    {
        var payloads = new[]
        {
            "{\"choices\":[{\"delta\":{\"content\":\"已收到图片\"}}]}"
        };

        await using var server = await FakeSseServer.StartAsync(payloads);
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        _ = await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "请看看这张图")
            {
                ImageAttachments =
                [
                    new ChatImageAttachment
                    {
                        FileName = "test.png",
                        FilePath = @"C:\\Temp\\test.png",
                        ContentType = "image/png",
                        DataUrl = "data:image/png;base64,AAAA"
                    }
                ]
            }],
            [],
            "test-key",
            server.Endpoint,
            "test-model"));

        server.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        var body = JObject.Parse(server.LastRequestBody!);
        var content = body["messages"]![0]!["content"]!.Should().BeOfType<JArray>().Subject;
        content.Should().HaveCount(2);
        content[0]!["type"]!.Value<string>().Should().Be("text");
        content[0]!["text"]!.Value<string>().Should().Be("请看看这张图");
        content[1]!["type"]!.Value<string>().Should().Be("image_url");
        content[1]!["image_url"]!["url"]!.Value<string>().Should().Be("data:image/png;base64,AAAA");
    }

    [Fact]
    public async Task StreamChatWithToolsAsync_WhenRequestFails_ShouldThrowRequestExceptionWithBody()
    {
        await using var server = await FakeSseServer.StartFailureAsync(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}");
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var action = async () => await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "你好")],
            [],
            "test-key",
            server.Endpoint,
            "test-model"));

        var exception = await action.Should().ThrowAsync<AiAnalysisRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exception.Which.ResponseBody.Should().Be("{\"error\":\"bad request\"}");
        exception.Which.RequestUri.Should().Be(server.Endpoint);
    }

    [Fact]
    public async Task StreamChatWithToolsAsync_WithVersionedBaseEndpoint_ShouldAppendOnlyChatCompletions()
    {
        var payloads = new[]
        {
            "{\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}"
        };

        await using var server = await FakeSseServer.StartAsync(payloads, "/compatible-mode/v1/chat/completions/");
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var chunks = await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "你好")],
            [],
            "test-key",
            server.BaseEndpoint,
            "test-model"));

        string.Concat(chunks.Where(chunk => chunk.IsContent).Select(chunk => chunk.ContentDelta)).Should().Be("你好");
        server.LastRequestUri.Should().Be(server.Endpoint);
    }

    [Fact]
    public async Task StreamChatWithToolsAsync_WhenProviderReturnsJsonMessage_ShouldExposeDetailedFailureMessage()
    {
        const string errorBody = "{\"error\":{\"message\":\"model not found\"}}";
        await using var server = await FakeSseServer.StartFailureAsync(HttpStatusCode.NotFound, errorBody, "/compatible-mode/v1/chat/completions/");
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var action = async () => await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "你好")],
            [],
            "test-key",
            server.BaseEndpoint,
            "missing-model"));

        var exception = await action.Should().ThrowAsync<AiAnalysisRequestException>();
        exception.Which.Message.Should().Contain("404 NotFound");
        exception.Which.Message.Should().Contain(server.Endpoint);
        exception.Which.Message.Should().Contain("model not found");
    }

    [Fact]
    public async Task StreamChatWithToolsAsync_WhenStreamReturnsErrorPayload_ShouldThrowRequestException()
    {
        var payloads = new[]
        {
            "{\"error\":{\"message\":\"provider stream rejected the request\"}}"
        };

        await using var server = await FakeSseServer.StartAsync(payloads);
        var service = new OpenAiAnalysisService(Mock.Of<ILogger<OpenAiAnalysisService>>());

        var action = async () => await CollectChunksAsync(service.StreamChatWithToolsAsync(
            [new ChatMessage("user", "你好")],
            [],
            "test-key",
            server.Endpoint,
            "test-model"));

        var exception = await action.Should().ThrowAsync<AiAnalysisRequestException>();
        exception.Which.Message.Should().Contain("provider stream rejected the request");
        exception.Which.ResponseBody.Should().Be("{\"error\":{\"message\":\"provider stream rejected the request\"}}");
        exception.Which.RequestUri.Should().Be(server.Endpoint);
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
            BaseEndpoint = endpoint[..endpoint.LastIndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase)];
        }

        public string Endpoint { get; }

        public string BaseEndpoint { get; }

        public string? LastRequestBody { get; private set; }

        public string? LastRequestUri { get; private set; }

        public static async Task<FakeSseServer> StartAsync(IReadOnlyList<string> payloads)
        {
            return await StartCoreAsync(HttpStatusCode.OK, payloads, failureBody: null, "/v1/chat/completions/");
        }

        public static async Task<FakeSseServer> StartAsync(IReadOnlyList<string> payloads, string endpointPath)
        {
            return await StartCoreAsync(HttpStatusCode.OK, payloads, failureBody: null, endpointPath);
        }

        public static async Task<FakeSseServer> StartFailureAsync(HttpStatusCode statusCode, string failureBody)
        {
            return await StartCoreAsync(statusCode, payloads: [], failureBody, "/v1/chat/completions/");
        }

        public static async Task<FakeSseServer> StartFailureAsync(HttpStatusCode statusCode, string failureBody, string endpointPath)
        {
            return await StartCoreAsync(statusCode, payloads: [], failureBody, endpointPath);
        }

        private static async Task<FakeSseServer> StartCoreAsync(HttpStatusCode statusCode, IReadOnlyList<string> payloads, string? failureBody, string endpointPath)
        {
            var port = GetFreePort();
            var normalizedPath = endpointPath.Trim();
            if (!normalizedPath.StartsWith('/'))
            {
                normalizedPath = "/" + normalizedPath;
            }

            if (!normalizedPath.EndsWith('/'))
            {
                normalizedPath += "/";
            }

            var prefix = $"http://127.0.0.1:{port}{normalizedPath}";
            var endpoint = prefix.TrimEnd('/');
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            FakeSseServer? server = null;

            var responseTask = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8, leaveOpen: false);
                if (server != null)
                {
                    server.LastRequestUri = context.Request.Url?.ToString()?.TrimEnd('/');
                    server.LastRequestBody = await reader.ReadToEndAsync();
                }

                context.Response.StatusCode = (int)statusCode;

                if (statusCode != HttpStatusCode.OK)
                {
                    context.Response.ContentType = "application/json";
                    await using var failureWriter = new StreamWriter(context.Response.OutputStream, new UTF8Encoding(false), leaveOpen: false);
                    await failureWriter.WriteAsync(failureBody ?? string.Empty);
                    await failureWriter.FlushAsync();
                    context.Response.Close();
                    return;
                }

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
            server = new FakeSseServer(listener, responseTask, endpoint);
            return server;
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