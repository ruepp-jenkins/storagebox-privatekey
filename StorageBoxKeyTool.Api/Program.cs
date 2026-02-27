using System.ComponentModel.DataAnnotations;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Domain;
using StorageBoxKeyTool.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var suppressNonLocalHostWarning = NonLocalHostWarningSettings.IsSuppressed(builder.Configuration);

builder.Services.AddSingleton<SshKeyGenerationService>();
builder.Services.AddSingleton<StorageBoxSftpService>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var urls = app.Urls
        .Select(ToDisplayUrl)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (urls.Length == 0)
    {
        Console.WriteLine("Hetzner StorageBox Helper started.");
        Console.WriteLine("Open in browser: http://localhost:8080");
        return;
    }

    foreach (var url in urls)
    {
        Console.WriteLine($"Hetzner StorageBox Helper started. Open in browser: {url}");
    }
});

app.UseDefaultFiles();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/ui/security-config", () =>
{
    return Results.Ok(new UiSecurityConfigResponse(suppressNonLocalHostWarning));
});

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

api.MapPost("/storagebox/backrest/check-ssh-login", (CheckSshLoginRequest request, StorageBoxSftpService storageBoxService) =>
{
    try
    {
        var response = storageBoxService.CheckSshLogin(request);
        return Results.Ok(response);
    }
    catch (ValidationException ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "validate", ex.Message, null);
    }
    catch (TimeoutException ex)
    {
        return CreateErrorResult(StatusCodes.Status504GatewayTimeout, "backrest-precheck", ex.Message, null);
    }
    catch (Exception ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "backrest-precheck", ex.Message, null);
    }
});

api.MapPost("/storagebox/backrest/apply", (ApplyBackrestResticRequest request, StorageBoxSftpService storageBoxService) =>
{
    try
    {
        var response = storageBoxService.ApplyBackrestResticCompatibility(request);
        return Results.Ok(response);
    }
    catch (ValidationException ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "backrest-precheck", ex.Message, null);
    }
    catch (TimeoutException ex)
    {
        return CreateErrorResult(StatusCodes.Status504GatewayTimeout, "backrest", ex.Message, null);
    }
    catch (Exception ex)
    {
        return CreateErrorResult(StatusCodes.Status400BadRequest, "backrest", ex.Message, null);
    }
});

app.MapFallbackToFile("index.html");

app.Run();

static IResult CreateErrorResult(int statusCode, string step, string message, string? detail)
{
    return Results.Json(new ApiErrorResponse(step, message, detail), statusCode: statusCode);
}

static string ToDisplayUrl(string rawUrl)
{
    if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
    {
        return rawUrl;
    }

    var host = uri.Host switch
    {
        "0.0.0.0" => "localhost",
        "::" => "localhost",
        "*" => "localhost",
        "+" => "localhost",
        _ => uri.Host
    };

    var builder = new UriBuilder(uri)
    {
        Host = host
    };

    return builder.Uri.ToString().TrimEnd('/');
}
