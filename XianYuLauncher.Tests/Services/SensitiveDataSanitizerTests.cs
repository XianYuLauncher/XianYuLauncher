using XianYuLauncher.Core.Helpers;

namespace XianYuLauncher.Tests.Services;

public sealed class SensitiveDataSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldRedact_CommonSensitiveValues()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.payload.signature";
        const string authorization = "Authorization: Bearer secret-access-token";
        const string jsonToken = "{\"access_token\":\"top-secret\",\"accessToken\":\"camel-secret\",\"clientToken\":\"client-secret\"}";
        const string encryptedToken = "ENC:QmFzZTY0RW5jb2RlZFRva2Vu";
        const string deviceCode = "code=ABCD-EFGH";

        var input = string.Join(Environment.NewLine, authorization, jsonToken, encryptedToken, jwt, deviceCode);

        var sanitized = SensitiveDataSanitizer.Sanitize(input);

        sanitized.Should().NotContain("secret-access-token");
        sanitized.Should().NotContain("top-secret");
        sanitized.Should().NotContain("camel-secret");
        sanitized.Should().NotContain("client-secret");
        sanitized.Should().NotContain(encryptedToken);
        sanitized.Should().NotContain(jwt);
        sanitized.Should().NotContain("ABCD-EFGH");
        sanitized.Should().Contain("[REDACTED]");
    }
}