using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dapr.Client;
using SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

namespace Computervision.Daos;

public class FileDao : IFileDao
{
    private readonly ILogger<FileDao> _logger;
    private readonly DaprClient _daprClient;

    public FileDao(ILogger<FileDao> logger, DaprClient daprClient)
    {
        _logger = logger;
        _daprClient = daprClient;
    }

    public async Task<string> GetPicture(string fileReference)
    {
        var request = _daprClient.CreateInvokeMethodRequest
        (
            HttpMethod.Get,
            "file-service",
            $"api/v1.0/File/{fileReference}"
        );

        var response = await _daprClient.InvokeMethodWithResponseAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception();
        }

        var fileResponse = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), AppJsonSerializerContext.Default.FileResponse);
        if (fileResponse is null)
        {
            throw new Exception();
        }

        return fileResponse.base64;
    }
}
