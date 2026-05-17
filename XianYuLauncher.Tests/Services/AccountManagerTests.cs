using Newtonsoft.Json;
using XianYuLauncher.Core.Contracts.Services;
using XianYuLauncher.Core.Helpers;
using XianYuLauncher.Core.Models;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class AccountManagerTests : IDisposable
{
    private readonly string _minecraftPath;

    public AccountManagerTests()
    {
        _minecraftPath = Path.Combine(Path.GetTempPath(), $"AccountManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_minecraftPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_minecraftPath))
        {
            Directory.Delete(_minecraftPath, true);
        }
    }

    [Fact]
    public async Task LoadAccountsAsync_ShouldRenameLegacyProfilesJson_ToAccountsJson()
    {
        var legacyProfilesPath = Path.Combine(_minecraftPath, MinecraftFileConsts.LegacyProfilesJson);
        var accountsPath = Path.Combine(_minecraftPath, MinecraftFileConsts.AccountsJson);
        var expectedAccount = new MinecraftAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "LegacyUser",
            IsActive = true,
            IsOffline = true,
        };

        await File.WriteAllTextAsync(
            legacyProfilesPath,
            JsonConvert.SerializeObject(new List<MinecraftAccount> { expectedAccount }, Formatting.Indented));

        var accountManager = new AccountManager(new TestFileService(_minecraftPath));

        var accounts = await accountManager.LoadAccountsAsync();

        Assert.Single(accounts);
        Assert.Equal(expectedAccount.Id, accounts[0].Id);
        Assert.Equal(expectedAccount.Name, accounts[0].Name);
        Assert.True(File.Exists(accountsPath));
        Assert.False(File.Exists(legacyProfilesPath));
    }

    [Fact]
    public async Task SaveAccountsAsync_ShouldStripMicrosoftRefreshToken_AndEncryptSensitiveTokens()
    {
        var accountsPath = Path.Combine(_minecraftPath, MinecraftFileConsts.AccountsJson);
        var accountManager = new AccountManager(new TestFileService(_minecraftPath));
        const string accessToken = "plain-access-token";
        const string refreshToken = "plain-refresh-token";
        const string clientToken = "plain-client-token";

        var profiles = new List<MinecraftAccount>
        {
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "MicrosoftUser",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ClientToken = clientToken,
                MicrosoftHomeAccountId = "msal-home-account-id",
                TokenType = "Bearer",
                ExpiresIn = 3600,
                IssueInstant = DateTime.UtcNow,
                NotAfter = DateTime.UtcNow.AddHours(1),
                IsOffline = false,
            },
        };

        await accountManager.SaveAccountsAsync(profiles);

        var json = await File.ReadAllTextAsync(accountsPath);
        json.Should().NotContain(accessToken);
        json.Should().NotContain(refreshToken);
        json.Should().NotContain(clientToken);

        var savedProfiles = JsonConvert.DeserializeObject<List<MinecraftAccount>>(json);
        savedProfiles.Should().ContainSingle();
        savedProfiles![0].AccessToken.Should().StartWith("ENC:");
        savedProfiles[0].ClientToken.Should().StartWith("ENC:");
        savedProfiles[0].RefreshToken.Should().BeEmpty();
        savedProfiles[0].MicrosoftHomeAccountId.Should().Be("msal-home-account-id");

        var loadedProfiles = await accountManager.LoadAccountsAsync();
        loadedProfiles.Should().ContainSingle();
        loadedProfiles[0].AccessToken.Should().Be(accessToken);
        loadedProfiles[0].ClientToken.Should().Be(clientToken);
        loadedProfiles[0].RefreshToken.Should().BeEmpty();
    }

    private sealed class TestFileService : IFileService
    {
        private string _minecraftPath;

        public TestFileService(string minecraftPath)
        {
            _minecraftPath = minecraftPath;
        }

        public event EventHandler<string>? MinecraftPathChanged;

        public string ReadText(string filePath) => File.ReadAllText(filePath);

        public void WriteText(string filePath, string content) => File.WriteAllText(filePath, content);

        public bool FileExists(string filePath) => File.Exists(filePath);

        public void CreateDirectory(string directoryPath) => Directory.CreateDirectory(directoryPath);

        public string GetAppDataPath() => _minecraftPath;

        public string GetMinecraftDataPath() => _minecraftPath;

        public void SetMinecraftDataPath(string path)
        {
            _minecraftPath = path;
            MinecraftPathChanged?.Invoke(this, path);
        }

        public string GetApplicationFolderPath() => _minecraftPath;

        public string GetLauncherCachePath() => _minecraftPath;

        public T? Read<T>(string folderPath, string fileName) => throw new NotSupportedException();

        public Task<T?> ReadAsync<T>(string folderPath, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Save<T>(string folderPath, string fileName, T content) => throw new NotSupportedException();

        public Task SaveAsync<T>(string folderPath, string fileName, T content, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Delete(string folderPath, string fileName) => throw new NotSupportedException();
    }
}