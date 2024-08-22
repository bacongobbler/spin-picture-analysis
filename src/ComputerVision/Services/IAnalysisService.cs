using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace ComputerVision.Services;

public interface IAnalysisService
{
    Task<List<Category>> AnalyzeImage(string base64);
}
