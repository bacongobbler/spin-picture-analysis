using Dapr.Client;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Logging;

namespace Computervision.Services;

public class AnalysisService : IAnalysisService
{
    private readonly ILogger<AnalysisService> _logger;
    private readonly DaprClient _daprClient;

    public AnalysisService(ILogger<AnalysisService> logger, DaprClient daprClient)
    {
        _logger = logger;
        _daprClient = daprClient;
    }

    public async Task<List<Category>> AnalyzeImage(string base64)
    {
        var cognitiveSecretKey = await GetSecret("secretstore", "cognitive-service-key");
        var cognitiveServiceUrl = await GetSecret("secretstore", "cognitive-service-url");

        _logger.LogInformation("got key {Key}", cognitiveSecretKey);
        _logger.LogInformation("got url {Url}", cognitiveServiceUrl);

        var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(cognitiveSecretKey))
        {
            Endpoint = cognitiveServiceUrl
        };

        var visualFeatureTypes = new List<VisualFeatureTypes?>
        {
            VisualFeatureTypes.Categories
        };

        var stream = new MemoryStream(Convert.FromBase64String(base64));
        var analysisResult = await client.AnalyzeImageInStreamAsync(stream, visualFeatureTypes);

        return analysisResult.Categories.ToList();
    }

    private async Task<string> GetSecret(string store, string key)
    {
        var secret = await _daprClient.GetSecretAsync(store, key);
        return secret.First().Value;
    }
}
