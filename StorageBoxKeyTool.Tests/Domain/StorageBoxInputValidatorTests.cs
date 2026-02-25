using System.ComponentModel.DataAnnotations;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Tests.Domain;

public sealed class StorageBoxInputValidatorTests
{
    [Fact]
    public void ValidateTarget_WithValidInput_ReturnsNormalizedTarget()
    {
        var result = StorageBoxInputValidator.ValidateTarget("U123456", 7);

        Assert.Equal("u123456", result.BaseUsername);
        Assert.Equal(7, result.SubId);
        Assert.Equal("u123456-sub7", result.Login);
        Assert.Equal("u123456-sub7.your-storagebox.de", result.Host);
        Assert.Equal(23, result.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("user-name")]
    [InlineData("user_name")]
    [InlineData("ABC!")]
    public void ValidateTarget_WithInvalidUsername_ThrowsValidationException(string username)
    {
        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidateTarget(username, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void ValidateTarget_WithOutOfRangeSubId_ThrowsValidationException(int subId)
    {
        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.ValidateTarget("u123456", subId));
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
    public void BuildComment_WithNullComment_ReturnsDerivedDefault()
    {
        var target = new StorageBoxTarget("u123456", 42);

        var result = StorageBoxInputValidator.BuildComment(target, null);

        Assert.Equal("u123456-sub42@u123456-sub42.your-storagebox.de", result);
    }

    [Fact]
    public void BuildComment_WithCustomComment_TrimsInput()
    {
        var target = new StorageBoxTarget("u123456", 42);

        var result = StorageBoxInputValidator.BuildComment(target, "  custom-comment  ");

        Assert.Equal("custom-comment", result);
    }

    [Fact]
    public void BuildComment_WithTooLongComment_ThrowsValidationException()
    {
        var target = new StorageBoxTarget("u123456", 42);
        var comment = new string('c', 129);

        Assert.Throws<ValidationException>(() => StorageBoxInputValidator.BuildComment(target, comment));
    }
}
