using StorageBoxKeyTool.Api.Services;

namespace StorageBoxKeyTool.Tests.Services;

public sealed class StorageBoxSftpServiceTests
{
    [Fact]
    public void BuildUploadResponseMessage_WithNoOptionsEnabled_ReturnsNoopMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            uploadSshPublicKey: false,
            sshChanged: false,
            backrestResticCompatible: false,
            rcloneConfigCreated: false
        );

        Assert.Equal("No remote changes were requested.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithUploadedSshKeyOnly_ReturnsUploadMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            uploadSshPublicKey: true,
            sshChanged: true,
            backrestResticCompatible: false,
            rcloneConfigCreated: false
        );

        Assert.Equal("SSH public key uploaded successfully.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithUnchangedSshAndBackrestConfigCreated_ReturnsCombinedMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            uploadSshPublicKey: true,
            sshChanged: false,
            backrestResticCompatible: true,
            rcloneConfigCreated: true
        );

        Assert.Equal("Remote SSH public key already matches the provided key. Created empty .config/rclone/rclone.conf.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithSkippedSshAndBackrestEnabled_ReturnsBackrestMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            uploadSshPublicKey: false,
            sshChanged: false,
            backrestResticCompatible: true,
            rcloneConfigCreated: false
        );

        Assert.Equal("SSH public key upload skipped. Backrest/restic compatibility is enabled.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithUnchangedSshOnly_ReturnsMatchMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(
            uploadSshPublicKey: true,
            sshChanged: false,
            backrestResticCompatible: false,
            rcloneConfigCreated: false
        );

        Assert.Equal("Remote SSH public key already matches the provided key.", result);
    }
}
