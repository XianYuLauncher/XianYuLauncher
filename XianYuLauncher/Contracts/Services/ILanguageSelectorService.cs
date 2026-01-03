namespace XianYuLauncher.Contracts.Services;

public interface ILanguageSelectorService
{
    string Language
    {
        get;
        set;
    }

    Task InitializeAsync();

    Task SetLanguageAsync(string language);
}
