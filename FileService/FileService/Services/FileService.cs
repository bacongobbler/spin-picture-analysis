using System.Text;
using System.Text.Json;
using CommunityToolkit.HighPerformance;
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

        _logger.LogInformation("creating binding request");
        var bindingRequest = new BindingRequest("file-entry-storage-binding", "create")
        {
            Data = JsonSerializer.SerializeToUtf8Bytes(request.Base64, AppJsonSerializerContext.Default.FileRequest),
            Metadata =
            {
                {"blobName", fileName}
            }
        };

        _logger.LogInformation("invoking binding");
        _ = await _daprClient.InvokeBindingAsync(bindingRequest);
        _logger.LogInformation("invoked binding");
        var fileResponse = new FileResponse(fileName);

        return fileResponse;
    }

    public async Task<FileRequest> Get(string fileName)
    {
        var bindingRequest = new BindingRequest("file-entry-storage-binding", "get")
        {
            Data = null,
            Metadata =
            {
                {"blobName", fileName}
            }
        };

        var blobResponse = await _daprClient.InvokeBindingAsync(bindingRequest);
        var fileRequest = new FileRequest(Convert.ToBase64String(blobResponse.Data.ToArray()), null);

        return fileRequest;
    }
}
