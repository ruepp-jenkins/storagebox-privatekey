using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StorageBoxKeyTool.Api.Domain;

internal static partial class StorageBoxInputValidator
{
    private const int MaxPasswordLength = 256;
    private const int MaxCommentLength = 128;

    public static StorageBoxTarget ValidateTarget(string username, int subId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ValidationException("Username is required.");
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();
        if (!UsernameRegex().IsMatch(normalizedUsername))
        {
            throw new ValidationException("Username must contain only lowercase letters and digits.");
        }

        if (subId < 0 || subId > 999999)
        {
            throw new ValidationException("Sub-ID must be between 0 and 999999.");
        }

        return new StorageBoxTarget(normalizedUsername, subId);
    }

    public static string ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("Storage Box password is required.");
        }

        if (password.Length > MaxPasswordLength)
        {
            throw new ValidationException($"Storage Box password must not exceed {MaxPasswordLength} characters.");
        }

        return password;
    }

    public static string BuildComment(StorageBoxTarget target, string? requestedComment)
    {
        var comment = string.IsNullOrWhiteSpace(requestedComment)
            ? $"{target.Login}@{target.Host}"
            : requestedComment.Trim();

        if (comment.Length > MaxCommentLength)
        {
            throw new ValidationException($"Key comment must not exceed {MaxCommentLength} characters.");
        }

        return comment;
    }

    [GeneratedRegex("^[a-z0-9]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();
}
