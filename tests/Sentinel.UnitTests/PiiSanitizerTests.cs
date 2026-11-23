using Sentinel.Shared.Security;

namespace Sentinel.UnitTests;

public class PiiSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldRedactSensitiveFields()
    {
        var raw = "Auth failed for user joao.silva@empresa.com.br (CPF 123.456.789-00) using Bearer eyJhbGciOiJIUzI1NiJ9.abc.def and Password=SuperSecret123;";

        var sanitized = PiiSanitizer.Sanitize(raw);

        Assert.DoesNotContain("joao.silva@empresa.com.br", sanitized);
        Assert.DoesNotContain("123.456.789-00", sanitized);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", sanitized);
        Assert.DoesNotContain("SuperSecret123", sanitized);
        Assert.Contains("[EMAIL_REDACTED]", sanitized);
        Assert.Contains("[CPF_REDACTED]", sanitized);
        Assert.Contains("Bearer [REDACTED]", sanitized);
        Assert.Contains("Password=[REDACTED]", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldReturnEmpty_WhenInputIsNullOrWhitespace()
    {
        Assert.Equal(string.Empty, PiiSanitizer.Sanitize(null));
        Assert.Equal(string.Empty, PiiSanitizer.Sanitize("   "));
    }
}
