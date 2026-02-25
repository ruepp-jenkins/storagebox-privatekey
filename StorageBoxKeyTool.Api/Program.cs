using System.ComponentModel.DataAnnotations;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Domain;
using StorageBoxKeyTool.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SshKeyGenerationService>();
builder.Services.AddSingleton<StorageBoxSftpService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapPost("/key/generate", async (GenerateKeyRequest request, SshKeyGenerationService keyService, CancellationToken cancellationToken) =>
{
    try
    {
        var response = await keyService.GenerateAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (ValidationException ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "validate", ex.Message, null);
    }
    catch (TimeoutException ex)
    {
        return CreateErrorResult(StatusCodes.Status504GatewayTimeout, "key-generation", ex.Message, null);
    }
    catch (Exception ex)
    {
        return CreateErrorResult(StatusCodes.Status500InternalServerError, "key-generation", ex.Message, null);
    }
});

api.MapPost("/storagebox/check", (CheckKeyRequest request, StorageBoxSftpService storageBoxService) =>
{
    try
    {
        var response = storageBoxService.CheckRemoteKey(request);
        return Results.Ok(response);
    }
    catch (ValidationException ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "validate", ex.Message, null);
    }
    catch (TimeoutException ex)
    {
        return CreateErrorResult(StatusCodes.Status504GatewayTimeout, "check", ex.Message, null);
    }
    catch (Exception ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "check", ex.Message, null);
    }
});

api.MapPost("/storagebox/upload", (UploadKeyRequest request, StorageBoxSftpService storageBoxService) =>
{
    try
    {
        var response = storageBoxService.UploadRemoteKey(request);
        return Results.Ok(response);
    }
    catch (ValidationException ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "validate", ex.Message, null);
    }
    catch (StorageBoxConflictException ex)
    {
        return CreateErrorResult(StatusCodes.Status409Conflict, "overwrite", ex.Message, null);
    }
    catch (TimeoutException ex)
    {
        return CreateErrorResult(StatusCodes.Status504GatewayTimeout, "upload", ex.Message, null);
    }
    catch (Exception ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "upload", ex.Message, null);
    }
});

app.MapFallbackToFile("index.html");

app.Run();

static IResult CreateErrorResult(int statusCode, string step, string message, string? detail)
{
    return Results.Json(new ApiErrorResponse(step, message, detail), statusCode: statusCode);
}
