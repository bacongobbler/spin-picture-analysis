using System.Net.Http.Json;
using System.Text.Json;
using Dapr.Client;
using FileService.Models;
using Microsoft.Extensions.Logging;
using SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

namespace FileService.Services;

public class FileService : IFileService
{
    private readonly ILogger<FileService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    // private readonly DaprClient _daprClient;

    public FileService(ILogger<FileService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // public FileService(ILogger<FileService> logger, DaprClient daprClient)
    // {
    //     _logger = logger;
    //     _daprClient = daprClient;
    // }

    public async Task<FileResponse> Save(FileRequest request)
    {
        var fileName = $"{Guid.NewGuid()}.{request.FileType}";
        var client = _httpClientFactory.CreateClient("dapr");

        var bindingRequest = new BindingRequest("file-entry-storage-binding", "create")
        {
            Data = JsonSerializer.SerializeToUtf8Bytes(request.Base64, AppJsonSerializerContext.Default.FileRequest),
            Metadata =
            {
                {"blobName", fileName}
            }
        };

        var response = await client.PostAsJsonAsync("/v1.0/bindings/file-entry-storage-binding", bindingRequest, AppJsonSerializerContext.Default.BindingRequest);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("received unsuccessful response from dapr sidecar: {ResponseCode} {ResponseContent}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new Exception($"failed to save {fileName}");
        }
        return new FileResponse(fileName);
    }

    public async Task<FileRequest> Get(string fileName)
    {
        var bindingRequest = new BindingRequest("file-entry-storage-binding", "get")
        {
            Metadata =
            {
                {"blobName", fileName}
            }
        };

        _logger.LogInformation("binding: {BindingRequest}", bindingRequest);

        // OPTION 1: using HTTPClient

        var client = _httpClientFactory.CreateClient("dapr");
        var response = await client.PostAsJsonAsync("/v1.0/bindings/file-entry-storage-binding", bindingRequest, AppJsonSerializerContext.Default.BindingRequest);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("received unsuccessful response from dapr sidecar: {ResponseCode} {ResponseContent}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new Exception($"failed to fetch {fileName}");
        }
        return new FileRequest(Convert.ToBase64String(await response.Content.ReadAsByteArrayAsync()), null);

        // OPTION 2: using DaprClient
        // TODO: use this option when System.Net.Sockets or HTTP/2 support lands (InvokeBindingAsync uses gRPC)

        // var blobResponse = await _daprClient.InvokeBindingAsync(bindingRequest);
        // var fileRequest = new FileRequest(Convert.ToBase64String(blobResponse.Data.ToArray()), null);
        // return fileRequest;
    }
}
