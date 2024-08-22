using System.Text.Json;
using Dapr.Client;

using ComputerVision.Models;

namespace ComputerVision.Daos;

public class FileDao : IFileDao
{
    private readonly DaprClient _daprClient;

    public FileDao(DaprClient daprClient)
    {
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
        var fileResponse = JsonSerializer.Deserialize<FileResponse>(await response.Content.ReadAsStringAsync());

        if (fileResponse is null)
        {
            throw new Exception();
        }

        return fileResponse.base64;
    }
}
