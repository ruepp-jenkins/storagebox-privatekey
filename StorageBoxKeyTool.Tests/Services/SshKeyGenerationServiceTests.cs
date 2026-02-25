using System.ComponentModel.DataAnnotations;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Services;

namespace StorageBoxKeyTool.Tests.Services;

public sealed class SshKeyGenerationServiceTests
{
    private readonly SshKeyGenerationService _service = new();

    [Fact]
    public async Task GenerateAsync_WithUnsupportedAlgorithm_ThrowsValidationException()
    {
        var request = new GenerateKeyRequest(
            "u123456",
            1,
            "dsa",
            null,
            null,
            null
        );

        await Assert.ThrowsAsync<ValidationException>(() => _service.GenerateAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(123)]
    [InlineData(999)]
    [InlineData(1024)]
    public async Task GenerateAsync_WithInvalidEcdsaKeySize_ThrowsValidationException(int keySize)
    {
        var request = new GenerateKeyRequest(
            "u123456",
            1,
            "ecdsa",
            keySize,
            null,
            null
        );

        await Assert.ThrowsAsync<ValidationException>(() => _service.GenerateAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(16384)]
    public async Task GenerateAsync_WithInvalidRsaKeySize_ThrowsValidationException(int keySize)
    {
        var request = new GenerateKeyRequest(
            "u123456",
            1,
            "rsa",
            keySize,
            null,
            null
        );

        await Assert.ThrowsAsync<ValidationException>(() => _service.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_WithTooLongPassphrase_ThrowsValidationException()
    {
        var request = new GenerateKeyRequest(
            "u123456",
            1,
            "ed25519",
            null,
            new string('p', 257),
            null
        );

        await Assert.ThrowsAsync<ValidationException>(() => _service.GenerateAsync(request, CancellationToken.None));
    }
}
