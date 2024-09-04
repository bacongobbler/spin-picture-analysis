using System.Text.Json;
using Dapr.Client;
using FileService.Models;
using Microsoft.Extensions.Logging;
using SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

namespace FileService.Services;

public class FileService : IFileService
{
    private readonly ILogger<FileService> _logger;
    private readonly DaprClient _daprClient;

    public FileService(ILogger<FileService> logger, DaprClient daprClient)
    {
        _logger = logger;
        _daprClient = daprClient;
    }

    public async Task<FileResponse> Save(FileRequest request)
    {
        var fileName = $"{Guid.NewGuid()}.{request.FileType}";

        var bindingRequest = new BindingRequest("file-entry-storage-binding", "create")
        {
            Data = JsonSerializer.SerializeToUtf8Bytes(request.Base64, AppJsonSerializerContext.Default.FileRequest),
            Metadata =
            {
                {"blobName", fileName}
            }
        };

        await _daprClient.InvokeBindingAsync(bindingRequest);
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

        var blobResponse = await _daprClient.InvokeBindingAsync(bindingRequest);
        var fileRequest = new FileRequest(Convert.ToBase64String(blobResponse.Data.ToArray()), null);
        return fileRequest;
    }
}
