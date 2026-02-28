using StorageBoxKeyTool.Api.Services;

namespace StorageBoxKeyTool.Tests.Services;

public sealed class StorageBoxSftpServiceTests
{
    [Fact]
    public void BuildUploadResponseMessage_WithChangedFalse_ReturnsIdenticalMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(changed: false);

        Assert.Equal("Remote SSH public key already matches the provided key.", result);
    }

    [Fact]
    public void BuildUploadResponseMessage_WithChangedTrue_ReturnsSuccessMessage()
    {
        var result = StorageBoxSftpService.BuildUploadResponseMessage(changed: true);

        Assert.Equal("SSH public key uploaded successfully.", result);
    }

    [Fact]
    public void BuildBackrestResponseMessage_WithSshAndRcloneChanges_ReturnsCombinedMessage()
    {
        var result = StorageBoxSftpService.BuildBackrestResponseMessage(sshChanged: true, rcloneConfigCreated: true);

        Assert.Equal("Configured backrest/restic compatibility in authorized_keys and created empty .config/rclone/rclone.conf.", result);
    }

    [Fact]
    public void BuildBackrestResponseMessage_WithOnlySshChange_ReturnsSshMessage()
    {
        var result = StorageBoxSftpService.BuildBackrestResponseMessage(sshChanged: true, rcloneConfigCreated: false);

        Assert.Equal("Configured backrest/restic compatibility in authorized_keys.", result);
    }

    [Fact]
    public void BuildBackrestResponseMessage_WithOnlyRcloneCreate_ReturnsRcloneMessage()
    {
        var result = StorageBoxSftpService.BuildBackrestResponseMessage(sshChanged: false, rcloneConfigCreated: true);

        Assert.Equal("Backrest/restic compatibility already existed in authorized_keys. Created empty .config/rclone/rclone.conf.", result);
    }

    [Fact]
    public void BuildBackrestResponseMessage_WithNoChanges_ReturnsAlreadyConfiguredMessage()
    {
        var result = StorageBoxSftpService.BuildBackrestResponseMessage(sshChanged: false, rcloneConfigCreated: false);

        Assert.Equal("Backrest/restic compatibility is already configured.", result);
    }

    [Fact]
    public void FindNextBackupNumber_WithNoExistingBackups_Returns1()
    {
        var files = new[] { "authorized_keys", ".", ".." };

        var result = StorageBoxSftpService.FindNextBackupNumber(files);

        Assert.Equal(1, result);
    }

    [Fact]
    public void FindNextBackupNumber_WithExistingBackup1_Returns2()
    {
        var files = new[] { "authorized_keys", "authorized_keys.backup.1" };

        var result = StorageBoxSftpService.FindNextBackupNumber(files);

        Assert.Equal(2, result);
    }

    [Fact]
    public void FindNextBackupNumber_WithGapInNumbering_ReturnsNextAfterHighest()
    {
        var files = new[] { "authorized_keys", "authorized_keys.backup.1", "authorized_keys.backup.3" };

        var result = StorageBoxSftpService.FindNextBackupNumber(files);

        Assert.Equal(4, result);
    }

    [Fact]
    public void FindNextBackupNumber_WithEmptyList_Returns1()
    {
        var files = Array.Empty<string>();

        var result = StorageBoxSftpService.FindNextBackupNumber(files);

        Assert.Equal(1, result);
    }
}
