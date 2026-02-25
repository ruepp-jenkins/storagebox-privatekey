using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Tests.Domain;

public sealed class PublicKeyUtilityTests
{
    [Fact]
    public void NormalizeOpenSshPublicKey_WithValidKey_ReturnsNormalizedKey()
    {
        const string input = "  ssh-ed25519 QUJD user@example  \n\nssh-ed25519 AAAA ignored";

        var result = PublicKeyUtility.NormalizeOpenSshPublicKey(input);

        Assert.Equal("ssh-ed25519 QUJD user@example", result);
    }

    [Fact]
    public void NormalizeOpenSshPublicKey_WithRfc4716Key_ThrowsValidationException()
    {
        const string input = "---- BEGIN SSH2 PUBLIC KEY ----\nComment: test\nAAAA\n---- END SSH2 PUBLIC KEY ----";

        Assert.Throws<ValidationException>(() => PublicKeyUtility.NormalizeOpenSshPublicKey(input));
    }

    [Fact]
    public void NormalizeOpenSshPublicKey_WithUnsupportedAlgorithm_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => PublicKeyUtility.NormalizeOpenSshPublicKey("ssh-dss QUJD comment"));
    }

    [Fact]
    public void NormalizeOpenSshPublicKey_WithInvalidBase64_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => PublicKeyUtility.NormalizeOpenSshPublicKey("ssh-ed25519 ??? comment"));
    }

    [Fact]
    public void ToAuthorizedKeysContent_AppendsTrailingNewLine()
    {
        const string normalized = "ssh-ed25519 QUJD user@example";

        var result = PublicKeyUtility.ToAuthorizedKeysContent(normalized);

        Assert.Equal("ssh-ed25519 QUJD user@example\n", result);
    }

    [Fact]
    public void NormalizeAuthorizedKeysContent_NormalizesLineEndingsAndTrims()
    {
        const string input = "ssh-ed25519 QUJD one\r\nssh-ed25519 QUJD two\r\n\r\n";

        var result = PublicKeyUtility.NormalizeAuthorizedKeysContent(input);

        Assert.Equal("ssh-ed25519 QUJD one\nssh-ed25519 QUJD two", result);
    }

    [Fact]
    public void ComputeSha256Fingerprint_ReturnsExpectedValue()
    {
        const string normalized = "ssh-ed25519 QUJD comment";
        var expectedHash = SHA256.HashData(Convert.FromBase64String("QUJD"));
        var expectedFingerprint = "SHA256:" + Convert.ToBase64String(expectedHash).TrimEnd('=');

        var result = PublicKeyUtility.ComputeSha256Fingerprint(normalized);

        Assert.Equal(expectedFingerprint, result);
    }

    [Fact]
    public void TryComputePrimaryFingerprint_WithValidEntry_ReturnsFingerprint()
    {
        const string content = "# comment\ninvalid line\nssh-ed25519 QUJD primary\nssh-ed25519 QUJD secondary\n";
        var expectedHash = SHA256.HashData(Convert.FromBase64String("QUJD"));
        var expectedFingerprint = "SHA256:" + Convert.ToBase64String(expectedHash).TrimEnd('=');

        var result = PublicKeyUtility.TryComputePrimaryFingerprint(content);

        Assert.Equal(expectedFingerprint, result);
    }

    [Fact]
    public void TryComputePrimaryFingerprint_WithNoValidKey_ReturnsNull()
    {
        const string content = "# comment only\nnot-a-key\n";

        var result = PublicKeyUtility.TryComputePrimaryFingerprint(content);

        Assert.Null(result);
    }
}
