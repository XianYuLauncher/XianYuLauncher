using XianYuLauncher.Contracts.Services;
using XianYuLauncher.Contracts.Services.Settings;
using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Services.Settings;

public class AISettingsDomainService : IAISettingsDomainService
{
    private const string EnableAIAnalysisKey = "EnableAIAnalysis";
    private const string AIApiEndpointKey = "AIApiEndpoint";
    private const string AIApiKeyKey = "AIApiKey";
    private const string AIModelKey = "AIModel";
    private const string AISystemPromptKey = "AISystemPrompt";

    private readonly ISettingsRepository _settingsRepository;

    public bool CurrentEnabled { get; private set; }

    public event EventHandler<bool>? EnabledChanged;

    public AISettingsDomainService(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<AISettingsState> LoadAsync()
    {
        var endpoint = await _settingsRepository.ReadAsync<string>(AIApiEndpointKey) ?? "https://api.openai.com";
        var model = await _settingsRepository.ReadAsync<string>(AIModelKey) ?? "gpt-3.5-turbo";
        var systemPrompt = await _settingsRepository.ReadAsync<string>(AISystemPromptKey) ?? string.Empty;
        var isEnabled = await _settingsRepository.ReadAsync<bool?>(EnableAIAnalysisKey) ?? false;
        CurrentEnabled = isEnabled;

        var storedKey = await _settingsRepository.ReadAsync<string>(AIApiKeyKey) ?? string.Empty;
        var plainKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(storedKey))
        {
            if (!TokenEncryption.IsEncrypted(storedKey))
            {
                var encrypted = TokenEncryption.Encrypt(storedKey);
                await _settingsRepository.SaveAsync(AIApiKeyKey, encrypted);
                plainKey = storedKey;
            }
            else
            {
                plainKey = TokenEncryption.Decrypt(storedKey);
            }
        }

        return new AISettingsState
        {
            IsEnabled = isEnabled,
            ApiEndpoint = endpoint,
            ApiKey = plainKey,
            Model = model,
            SystemPrompt = systemPrompt
        };
    }

    public void PublishEnabledState(bool value)
    {
        if (CurrentEnabled == value)
        {
            return;
        }

        CurrentEnabled = value;
        EnabledChanged?.Invoke(this, value);
    }

    public Task SaveEnabledAsync(bool value)
    {
        PublishEnabledState(value);
        return _settingsRepository.SaveAsync(EnableAIAnalysisKey, value);
    }

    public Task SaveApiEndpointAsync(string value)
    {
        return _settingsRepository.SaveAsync(AIApiEndpointKey, value);
    }

    public Task SaveApiKeyAsync(string value)
    {
        var encrypted = TokenEncryption.Encrypt(value);
        return _settingsRepository.SaveAsync(AIApiKeyKey, encrypted);
    }

    public Task SaveModelAsync(string value)
    {
        return _settingsRepository.SaveAsync(AIModelKey, value);
    }

    public Task SaveSystemPromptAsync(string value)
    {
        return _settingsRepository.SaveAsync(AISystemPromptKey, value);
    }
}
