using System.Net;
using System.Net.Http.Json;
using StorageBoxKeyTool.Client.Models;

namespace StorageBoxKeyTool.Client.Services;

public sealed class StorageBoxApiClient(HttpClient httpClient)
{
    public Task<GenerateKeyResponse> GenerateKeyAsync(GenerateKeyRequest request, CancellationToken cancellationToken)
    {
        return PostAsync<GenerateKeyResponse>("api/key/generate", request, cancellationToken);
    }

    public Task<CheckKeyResponse> CheckRemoteKeyAsync(CheckKeyRequest request, CancellationToken cancellationToken)
    {
        return PostAsync<CheckKeyResponse>("api/storagebox/check", request, cancellationToken);
    }

    public Task<UploadKeyResponse> UploadRemoteKeyAsync(UploadKeyRequest request, CancellationToken cancellationToken)
    {
        return PostAsync<UploadKeyResponse>("api/storagebox/upload", request, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TResponse>(string relativePath, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(relativePath, payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
            if (result is null)
            {
                throw new ApiClientException("api", "The API returned an empty response.", response.StatusCode, null);
            }

            return result;
        }

        var apiError = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
        if (apiError is not null)
        {
            throw new ApiClientException(apiError.Step, apiError.Message, response.StatusCode, apiError.Detail);
        }

        var rawError = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ApiClientException("api", rawError, response.StatusCode, null);
    }
}

public sealed class ApiClientException(
    string step,
    string message,
    HttpStatusCode statusCode,
    string? detail) : Exception(message)
{
    public string Step { get; } = step;

    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Detail { get; } = detail;
}
