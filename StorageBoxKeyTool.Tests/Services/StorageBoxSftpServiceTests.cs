using StorageBoxKeyTool.Api.Services;

namespace StorageBoxKeyTool.Tests.Services;

public sealed class StorageBoxSftpServiceTests
{
    [Fact]
    public void BuildIdenticalResponseMessage_WithRcloneOptionDisabled_ReturnsAuthorizedKeysMessage()
    {
        var result = StorageBoxSftpService.BuildIdenticalResponseMessage(
            createEmptyRcloneConfig: false,
            rcloneConfigCreated: false
        );

        Assert.Equal("Remote authorized_keys already matches the provided key.", result);
    }

    [Fact]
    public void BuildIdenticalResponseMessage_WithCreatedRcloneConfig_ReturnsCreatedMessage()
    {
        var result = StorageBoxSftpService.BuildIdenticalResponseMessage(
            createEmptyRcloneConfig: true,
            rcloneConfigCreated: true
        );

        Assert.Equal("Remote authorized_keys already matches the provided key. Created empty rclone/rclone.conf.", result);
    }

    [Fact]
    public void BuildIdenticalResponseMessage_WithExistingRcloneConfig_ReturnsBaseMessage()
    {
        var result = StorageBoxSftpService.BuildIdenticalResponseMessage(
            createEmptyRcloneConfig: true,
            rcloneConfigCreated: false
        );

        Assert.Equal("Remote authorized_keys already matches the provided key.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithCreatedRcloneConfig_ReturnsCreatedMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            createEmptyRcloneConfig: true,
            rcloneConfigCreated: true
        );

        Assert.Equal("SSH public key uploaded successfully. Created empty rclone/rclone.conf.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithExistingRcloneConfig_ReturnsBaseMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            createEmptyRcloneConfig: true,
            rcloneConfigCreated: false
        );

        Assert.Equal("SSH public key uploaded successfully.", result);
    }
}
