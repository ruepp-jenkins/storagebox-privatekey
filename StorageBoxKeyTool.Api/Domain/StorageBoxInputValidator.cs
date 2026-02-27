using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StorageBoxKeyTool.Api.Domain;

internal static partial class StorageBoxInputValidator
{
    public const string DefaultRootDirectory = "/home";
    private const int MaxPasswordLength = 256;
    private const int MaxCommentLength = 128;

    public static StorageBoxTarget ValidateTarget(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ValidationException("Username is required.");
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();
        if (!UsernameRegex().IsMatch(normalizedUsername))
        {
            throw new ValidationException("Username must use format <base>-sub<id> with lowercase letters and digits (for example u123456-sub12).");
        }

        return new StorageBoxTarget(normalizedUsername);
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

    public static string ValidateRootDirectory(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return DefaultRootDirectory;
        }

        var normalizedRootDirectory = rootDirectory.Trim();
        if (normalizedRootDirectory.Length > 1)
        {
            normalizedRootDirectory = normalizedRootDirectory.TrimEnd('/');
        }

        if (string.Equals(normalizedRootDirectory, DefaultRootDirectory, StringComparison.Ordinal))
        {
            return normalizedRootDirectory;
        }

        if (!normalizedRootDirectory.StartsWith("/home/", StringComparison.Ordinal))
        {
            throw new ValidationException("Root directory must be /home or start with /home/.");
        }

        return normalizedRootDirectory;
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

    [GeneratedRegex("^[a-z0-9]{1,32}-sub[0-9]{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernameRegex();
}
