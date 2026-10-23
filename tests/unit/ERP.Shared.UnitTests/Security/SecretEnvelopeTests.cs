using ERP.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.Shared.UnitTests.Security;

public class SecretEnvelopeTests
{
    private static AesSecretProtector Protector(string key = "test-key-1") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:SettingsEncryptionKey"] = key }).Build());

    [Fact]
    public void Seal_ThenOpen_ReturnsTheOriginal_AndTheSealedValueIsMarked()
    {
        var protector = Protector();

        var sealedValue = SecretEnvelope.Seal(protector, "123456");

        sealedValue.Should().StartWith(SecretEnvelope.Prefix).And.NotContain("123456");
        SecretEnvelope.IsSealed(sealedValue).Should().BeTrue();
        SecretEnvelope.Open(protector, sealedValue).Should().Be("123456");
    }

    [Fact]
    public void Open_ALegacyPlaintext_IsReturnedAsItIs()
    {
        // Rows and messages written before encryption existed have no marker; base64-looking plaintext must not be "decrypted".
        SecretEnvelope.Open(Protector(), "c2VjcmV0").Should().Be("c2VjcmV0");
        SecretEnvelope.Open(Protector(), "https://shop.test/reset?token=abc").Should().Be("https://shop.test/reset?token=abc");
    }

    [Fact]
    public void Seal_AnAlreadySealedValue_IsNotEncryptedTwice()
    {
        var protector = Protector();
        var once = SecretEnvelope.Seal(protector, "secret");

        SecretEnvelope.Seal(protector, once).Should().Be(once);
    }

    [Fact]
    public void Seal_UsesAFreshIvEachTime()
    {
        var protector = Protector();

        SecretEnvelope.Seal(protector, "same").Should().NotBe(SecretEnvelope.Seal(protector, "same"));
    }

    [Fact]
    public void Open_WithAnotherKey_Fails_InsteadOfReturningGarbage()
    {
        var sealedValue = SecretEnvelope.Seal(Protector("key-a"), "secret");

        var act = () => SecretEnvelope.Open(Protector("key-b"), sealedValue);

        act.Should().Throw<Exception>();
    }
}
