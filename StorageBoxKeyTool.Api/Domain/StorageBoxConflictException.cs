namespace StorageBoxKeyTool.Api.Domain;

public sealed class StorageBoxConflictException(string message) : Exception(message);
