using Microsoft.Identity.Client;
using XianYuLauncher.Core.Services;

namespace XianYuLauncher.Tests.Services;

public sealed class MicrosoftAuthServiceTests
{
    [Fact]
    public void ResolveMsalAccountCandidate_ShouldReturnExactMatch_WhenHomeAccountIdExists()
    {
        var expectedAccount = new FakeMsalAccount("home-account-1");
        var accounts = new List<IAccount>
        {
            new FakeMsalAccount("home-account-0"),
            expectedAccount,
        };

        var result = MicrosoftAuthService.ResolveMsalAccountCandidate(accounts, "home-account-1", hasPackageIdentity: true);

        result.Account.Should().BeSameAs(expectedAccount);
        result.UseOperatingSystemAccount.Should().BeFalse();
    }

    [Fact]
    public void ResolveMsalAccountCandidate_ShouldFallbackToSingleCachedAccount_WhenHomeAccountIdMisses()
    {
        var onlyAccount = new FakeMsalAccount("cached-home-account");
        var accounts = new List<IAccount> { onlyAccount };

        var result = MicrosoftAuthService.ResolveMsalAccountCandidate(accounts, "missing-home-account", hasPackageIdentity: true);

        result.Account.Should().BeSameAs(onlyAccount);
        result.UseOperatingSystemAccount.Should().BeFalse();
    }

    [Fact]
    public void ResolveMsalAccountCandidate_ShouldUseOperatingSystemAccount_WhenNoCacheAndPackaged()
    {
        var result = MicrosoftAuthService.ResolveMsalAccountCandidate(Array.Empty<IAccount>(), microsoftHomeAccountId: null, hasPackageIdentity: true);

        result.Account.Should().BeNull();
        result.UseOperatingSystemAccount.Should().BeTrue();
    }

    [Fact]
    public void ResolveMsalAccountCandidate_ShouldRequireInteractiveLogin_WhenNoCacheAndNoPackageIdentity()
    {
        var result = MicrosoftAuthService.ResolveMsalAccountCandidate(Array.Empty<IAccount>(), microsoftHomeAccountId: null, hasPackageIdentity: false);

        result.Account.Should().BeNull();
        result.UseOperatingSystemAccount.Should().BeFalse();
    }

    private sealed class FakeMsalAccount : IAccount
    {
        public FakeMsalAccount(string homeAccountId)
        {
            HomeAccountId = new AccountId(homeAccountId, homeAccountId, "tenant");
            Username = $"{homeAccountId}@example.com";
        }

        public string Username { get; }

        public string Environment => "login.microsoftonline.com";

        public AccountId HomeAccountId { get; }
    }
}