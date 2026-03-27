using System;
using System.Threading.Tasks;

namespace XianYuLauncher.Contracts.Services.Settings;

public sealed class AiSettingsState
{
    public bool IsEnabled { get; init; }

    public string ApiEndpoint { get; init; } = "https://api.openai.com";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gpt-3.5-turbo";

    public string SystemPrompt { get; init; } = string.Empty;
}

public interface IAiSettingsDomainService
{
    bool CurrentEnabled { get; }

    event EventHandler<bool>? EnabledChanged;

    Task<AiSettingsState> LoadAsync();

    void PublishEnabledState(bool value);

    Task SaveEnabledAsync(bool value);

    Task SaveApiEndpointAsync(string value);

    Task SaveApiKeyAsync(string value);

    Task SaveModelAsync(string value);

    Task SaveSystemPromptAsync(string value);
}
