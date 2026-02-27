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
    public void ToBackrestResticAuthorizedKeysContent_WithValidInput_ReturnsExpectedFormat()
    {
        const string normalized = "ssh-ed25519 QUJD user@example";

        var result = PublicKeyUtility.ToBackrestResticAuthorizedKeysContent(
            normalized,
            "u123456-sub12",
            "u123456-sub12.your-storagebox.de",
            "/home"
        );

        Assert.Contains("---- BEGIN SSH2 PUBLIC KEY ----", result);
        Assert.Contains("Comment: \"u123456-sub12@u123456-sub12.your-storagebox.de\"", result);
        Assert.Contains("---- END SSH2 PUBLIC KEY ----", result);
        Assert.Contains("command=\"rclone serve restic --stdio /home\" ssh-ed25519 QUJD u123456-sub12@u123456-sub12.your-storagebox.de", result);
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
    public void ContainsPublicKey_WithCommandPrefixedLine_ReturnsTrue()
    {
        const string normalized = "ssh-ed25519 QUJD expected@host";
        const string content = "command=\"rclone serve restic --stdio /home\" ssh-ed25519 QUJD test@host\n";

        var result = PublicKeyUtility.ContainsPublicKey(content, normalized);

        Assert.True(result);
    }

    [Fact]
    public void ContainsBackrestResticEntry_WithMatchingRootAndKey_ReturnsTrue()
    {
        const string normalized = "ssh-ed25519 QUJD expected@host";
        const string content = "command=\"rclone serve restic --stdio /home/data\" ssh-ed25519 QUJD test@host\n";

        var result = PublicKeyUtility.ContainsBackrestResticEntry(content, normalized, "/home/data");

        Assert.True(result);
    }

    [Fact]
    public void ContainsBackrestResticEntry_WithDifferentRoot_ReturnsFalse()
    {
        const string normalized = "ssh-ed25519 QUJD expected@host";
        const string content = "command=\"rclone serve restic --stdio /home/data\" ssh-ed25519 QUJD test@host\n";

        var result = PublicKeyUtility.ContainsBackrestResticEntry(content, normalized, "/home/other");

        Assert.False(result);
    }

    [Fact]
    public void ContainsSsh2PublicKeyBlock_WithMatchingPayload_ReturnsTrue()
    {
        const string normalized = "ssh-ed25519 QUJD expected@host";
        const string content = "---- BEGIN SSH2 PUBLIC KEY ----\nComment: \"expected@host\"\nQUJD\n---- END SSH2 PUBLIC KEY ----\n";

        var result = PublicKeyUtility.ContainsSsh2PublicKeyBlock(content, normalized);

        Assert.True(result);
    }

    [Fact]
    public void ContainsSsh2PublicKeyBlock_WithDifferentPayload_ReturnsFalse()
    {
        const string normalized = "ssh-ed25519 QUJD expected@host";
        const string content = "---- BEGIN SSH2 PUBLIC KEY ----\nComment: \"expected@host\"\nAAAA\n---- END SSH2 PUBLIC KEY ----\n";

        var result = PublicKeyUtility.ContainsSsh2PublicKeyBlock(content, normalized);

        Assert.False(result);
    }

    [Fact]
    public void MergeAuthorizedKeysContent_WithBackrestMode_ReplacesManagedKeyAndKeepsOtherLines()
    {
        const string existing = "# keep\n---- BEGIN SSH2 PUBLIC KEY ----\nComment: \"old@host\"\nT0xE\n---- END SSH2 PUBLIC KEY ----\ncommand=\"rclone serve restic --stdio /home\" ssh-ed25519 T0xE old@host\n# tail\n";
        const string newKey = "ssh-ed25519 TkVX new@host";

        var result = PublicKeyUtility.MergeAuthorizedKeysContent(
            existing,
            newKey,
            "u123456-sub12",
            "u123456-sub12.your-storagebox.de",
            backrestResticCompatible: true,
            rootDirectory: "/home"
        );

        Assert.Contains("# keep", result);
        Assert.Contains("# tail", result);
        Assert.Contains("command=\"rclone serve restic --stdio /home\" ssh-ed25519 TkVX u123456-sub12@u123456-sub12.your-storagebox.de", result);
        Assert.DoesNotContain("ssh-ed25519 T0xE", result);
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
    public void TryComputePrimaryFingerprint_WithCommandPrefixedEntry_ReturnsFingerprint()
    {
        const string content = "command=\"rclone serve restic --stdio /home\" ssh-ed25519 QUJD user@host\n";
        var expectedHash = SHA256.HashData(Convert.FromBase64String("QUJD"));
        var expectedFingerprint = "SHA256:" + Convert.ToBase64String(expectedHash).TrimEnd('=');

        var result = PublicKeyUtility.TryComputePrimaryFingerprint(content);

        Assert.Equal(expectedFingerprint, result);
    }

    [Fact]
    public void TryExtractPrimaryPublicKey_WithCommandPrefixedEntry_ReturnsNormalizedKey()
    {
        const string content = "command=\"rclone serve restic --stdio /home\" ssh-ed25519 QUJD user@host\n";

        var result = PublicKeyUtility.TryExtractPrimaryPublicKey(content);

        Assert.Equal("ssh-ed25519 QUJD user@host", result);
    }

    [Fact]
    public void TryComputePrimaryFingerprint_WithNoValidKey_ReturnsNull()
    {
        const string content = "# comment only\nnot-a-key\n";

        var result = PublicKeyUtility.TryComputePrimaryFingerprint(content);

        Assert.Null(result);
    }
}
