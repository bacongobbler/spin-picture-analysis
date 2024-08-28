using System.Net.Http.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Logging;
using SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

namespace Computervision.Services;

public class AnalysisService : IAnalysisService
{
    private readonly ILogger<AnalysisService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AnalysisService(ILogger<AnalysisService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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
        var httpClient = _httpClientFactory.CreateClient("dapr");
        var response = await httpClient.GetAsync($"/v1.0/secrets/{store}/{key}");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("received unsuccessful response from dapr sidecar: {ResponseCode} {ResponseContent}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new Exception("failed to analyze image");
        }

        var dict = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.DictionaryStringString);
        if (dict is null)
        {
            _logger.LogWarning("could not parse response as secret: {ResponseCode} {ResponseContent}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new Exception("failed to analyze image");
        }

        return dict.First().Value;
    }
}
