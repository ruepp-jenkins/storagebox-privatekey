using System.ComponentModel.DataAnnotations;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Tests.Domain;

public sealed class StorageBoxInputValidatorTests
{
    [Fact]
    public void ValidateTarget_WithValidInput_ReturnsNormalizedTarget()
    {
        var result = StorageBoxInputValidator.ValidateTarget("U123456-SUB7");

        Assert.Equal("u123456-sub7", result.Login);
        Assert.Equal("u123456-sub7.your-storagebox.de", result.Host);
        Assert.Equal(23, result.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("u123456")]
    [InlineData("u123456-sub")]
    [InlineData("u123456-sub1000000")]
    [InlineData("user-name-sub1")]
    [InlineData("user_name")]
    [InlineData("ABC!")]
    public void ValidateTarget_WithInvalidUsername_ThrowsValidationException(string username)
    {
        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidateTarget(username));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ValidatePassword_WithEmptyInput_ThrowsValidationException(string password)
    {
        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidatePassword(password));
    }

    [Fact]
    public void ValidatePassword_WithTooLongInput_ThrowsValidationException()
    {
        var password = new string('p', 257);

        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidatePassword(password));
    }

    [Fact]
    public void ValidatePassword_WithValidInput_ReturnsOriginalValue()
    {
        const string password = "A-Very-Secret-Passphrase";

        var result = StorageBoxInputValidator.ValidatePassword(password);

        Assert.Equal(password, result);
    }

    [Fact]
    public void ValidateRootDirectory_WithNullInput_ReturnsDefault()
    {
        var result = StorageBoxInputValidator.ValidateRootDirectory(null);

        Assert.Equal("/home", result);
    }

    [Fact]
    public void ValidateRootDirectory_WithValidCustomInput_ReturnsNormalizedRoot()
    {
        var result = StorageBoxInputValidator.ValidateRootDirectory(" /home/backups/ ");

        Assert.Equal("/home/backups", result);
    }

    [Theory]
    [InlineData("/tmp")]
    [InlineData("home/test")]
    [InlineData("/home2")]
    public void ValidateRootDirectory_WithInvalidInput_ThrowsValidationException(string rootDirectory)
    {
        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidateRootDirectory(rootDirectory));
    }

    [Fact]
    public void BuildComment_WithNullComment_ReturnsDerivedDefault()
    {
        var target = new StorageBoxTarget("u123456-sub42");

        var result = StorageBoxInputValidator.BuildComment(target, null);

        Assert.Equal("u123456-sub42@u123456-sub42.your-storagebox.de", result);
    }

    [Fact]
    public void BuildComment_WithCustomComment_TrimsInput()
    {
        var target = new StorageBoxTarget("u123456-sub42");

        var result = StorageBoxInputValidator.BuildComment(target, "  custom-comment  ");

        Assert.Equal("custom-comment", result);
    }

    [Fact]
    public void BuildComment_WithTooLongComment_ThrowsValidationException()
    {
        var target = new StorageBoxTarget("u123456-sub42");
        var comment = new string('c', 129);

        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.BuildComment(target, comment));
    }
}
