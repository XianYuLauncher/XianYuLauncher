using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Models;

namespace XianYuLauncher.Core.Services
{
    public class OpenAiAnalysisService : IAIAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiAnalysisService> _logger;

        public OpenAiAnalysisService(ILogger<OpenAiAnalysisService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // AI calls can be slow
        }

        public async Task<string> AnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese")
        {
            try
            {
                var sb = new StringBuilder();
                await foreach (var chunk in StreamAnalyzeLogAsync(logContent, apiKey, endpoint, model, language))
                {
                    sb.Append(chunk);
                }

                var result = sb.ToString();
                if (string.IsNullOrWhiteSpace(result))
                {
                    return "AI returned an empty response.";
                }

                return result;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AI analysis");
                return $"Analysis Error: {ex.Message}";
            }
        }

        public async IAsyncEnumerable<string> StreamAnalyzeLogAsync(string logContent, string apiKey, string endpoint, string model, string language = "Chinese")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return "API Key is missing.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://api.openai.com/v1/chat/completions";
            }
            else if (!endpoint.EndsWith("/v1/chat/completions") && !endpoint.EndsWith("/chat/completions"))
            {
                if (endpoint.EndsWith("/"))
                {
                    endpoint += "v1/chat/completions";
                }
                else
                {
                    endpoint += "/v1/chat/completions";
                }
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-3.5-turbo";
            }

            if (logContent.Length > 15000)
            {
                logContent = logContent.Substring(logContent.Length - 15000);
                logContent = "[...Log Truncated...] " + logContent;
            }

            var systemPrompt =
                "You are an expert Minecraft technical support agent. " +
                $"Analyze the following Minecraft crash log and explain in simple localized language ({language}) what caused the crash and how to fix it. " +
                "Be concise and reference specific mods or config files if they are responsible. " +
                "\n\n" +
                "If you need to suggest actionable fixes, output a JSON array inside <TOOL_CALLS>...</TOOL_CALLS> at the END of your reply. " +
                "Each item should follow: {\"type\": string, \"buttonText\": string, \"parameters\": { ... } }. " +
                "Available types: searchModrinthProject, deleteMod, switchJavaForVersion. " +
                "Parameters: searchModrinthProject => {query, projectType?, loader?}; deleteMod => {modId}; switchJavaForVersion => {}. " +
                "Do not include any other keys in tool objects.";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = logContent }
                },
                temperature = 0.5,
                stream = true
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string? error = null;
            HttpResponseMessage? response = null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = content;
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during streaming AI analysis");
                error = $"Analysis Error: {ex.Message}";
            }

            if (error != null)
            {
                yield return error;
                yield break;
            }

            if (response == null)
            {
                yield return "Analysis Error: Empty response.";
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogError($"AI Request Failed: {response.StatusCode} - {responseString}");
                yield return $"AI Request Failed: {response.StatusCode}. Please check your API Key and Endpoint config.";
                yield break;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(responseStream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line.Substring("data:".Length).Trim();
                if (data == "[DONE]")
                {
                    yield break;
                }

                if (TryExtractContentDelta(data, out var contentDelta))
                {
                    yield return contentDelta;
                }
            }
        }

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, string apiKey, string endpoint, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return "API Key is missing.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = "https://api.openai.com/v1/chat/completions";
            }
            else if (!endpoint.EndsWith("/v1/chat/completions") && !endpoint.EndsWith("/chat/completions"))
            {
                if (endpoint.EndsWith("/"))
                {
                    endpoint += "v1/chat/completions";
                }
                else
                {
                    endpoint += "/v1/chat/completions";
                }
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-3.5-turbo";
            }

            // Convert to anonymous object list to ensure correct serialization
            var apiMessages = new List<object>();
            foreach(var msg in messages)
            {
                apiMessages.Add(new { role = msg.Role, content = msg.Content });
            }

            var requestBody = new
            {
                model = model,
                messages = apiMessages,
                temperature = 0.5,
                stream = true
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string? error = null;
            HttpResponseMessage? response = null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = content;
                
                System.Diagnostics.Debug.WriteLine($"[AI Service] Sending request to {endpoint}");
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                System.Diagnostics.Debug.WriteLine($"[AI Service] Response received: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI Service] Request failed: {ex.Message}");
                _logger.LogError(ex, "Error during streaming AI chat");
                error = $"Chat Error: {ex.Message}";
            }

            if (error != null)
            {
                yield return error;
                yield break;
            }

            if (response == null)
            {
                 yield return "Chat Error: Empty response.";
                 yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[AI Service] Error response body: {responseString}");
                _logger.LogError($"AI Request Failed: {response.StatusCode} - {responseString}");
                yield return $"AI Request Failed: {response.StatusCode}.";
                yield break;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(responseStream);

            System.Diagnostics.Debug.WriteLine("[AI Service] Stream opened. Reading lines...");
            int lineCount = 0;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Inspect raw line for debugging
                if (line.StartsWith("data:")) 
                {
                     var debugPreview = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                     System.Diagnostics.Debug.WriteLine($"[AI Service] RAW: {debugPreview}");
                }

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

                var data = line.Substring("data:".Length).Trim();
                if (data == "[DONE]") 
                {
                    System.Diagnostics.Debug.WriteLine("[AI Service] Stream [DONE] received.");
                    yield break;
                }

                if (TryExtractContentDelta(data, out var contentDelta))
                {
                    lineCount++;
                    yield return contentDelta;
                }
                else
                {
                     // Check if it's a reasoning_content or just empty
                     try {
                        var parsedJson = JObject.Parse(data);
                        var thinking = parsedJson["choices"]?[0]?["delta"]?["reasoning_content"]?.ToString();
                        if (!string.IsNullOrEmpty(thinking)) {
                            // System.Diagnostics.Debug.WriteLine("[AI Service] Skipping reasoning_content chunk.");
                        }
                     } catch {}
                }
            }
            System.Diagnostics.Debug.WriteLine($"[AI Service] EndOfStream reached. Total Content Lines: {lineCount}");
        }

        private bool TryExtractContentDelta(string data, out string contentDelta)
        {
            contentDelta = string.Empty;
            try
            {
                var jsonResponse = JObject.Parse(data);
                contentDelta = jsonResponse["choices"]?[0]?["delta"]?["content"]?.ToString()
                              ?? jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString()
                              ?? string.Empty;

                return !string.IsNullOrEmpty(contentDelta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse streaming AI response chunk");
                return false;
            }
        }
    }
}
