using ComputerVision.Daos;
using ComputerVision.Models;
using ComputerVision.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spin.Http;
using SpinHttpWorld.wit.imports.wasi.http.v0_2_0;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

public class IncomingHandlerImpl : IIncomingHandler
{
    [RequiresUnreferencedCode("WASI")]
    public static void Handle(ITypes.IncomingRequest request, ITypes.ResponseOutparam response)
    {
        var builder = WebApplication.CreateSlimBuilder([]);

        // workaround - use in-memory collection to modify default app settings
        //
        // TODO: read from Azure App Configuration in production
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Debug",
            ["Logging:LogLevel:Microsoft"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Information",
        });

        // Add WASI-related services to the container.
        builder.Services.AddSingleton<IServer, WasiHttpServer>();
        builder.Logging.ClearProviders();
        builder.Logging
            .AddProvider(new WasiLoggingProvider());


        // Add assistive services - not business critical
        builder.Services.AddHealthChecks();
        builder.Services.AddOpenApi();

        // Add serialization/deserialization
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        // Add Core business services
        builder.Services.AddHttpClient();
        builder.Services.AddDaprClient();
        builder.Services.AddTransient<IFileDao, FileDao>();
        builder.Services.AddTransient<IComputerVisionService, ComputerVisionService>();
        builder.Services.AddTransient<IAnalysisService, AnalysisService>();

        var app = builder.Build();

        app.UseRouting();
        app.MapHealthChecks("/health");
        app.MapOpenApi();

        app.UseCloudEvents();

        app.MapPost("/api/v1.0/computervision", async context =>
        {
            var computerVisionService = context.RequestServices.GetRequiredService<IComputerVisionService>();
            var fileReference = "foo";
            app.Logger.LogInformation("processing image {FileReference}", fileReference);
            await computerVisionService.ProcessImage(fileReference);
            app.Logger.LogInformation("processed image {FileReference}", fileReference);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        Func<Task> task = async () =>
        {
            await app.StartAsync();
            await WasiHttpServer.HandleRequestAsync(request, response);
        };

        RequestHandler.Run(task());
    }
}

[JsonSerializable(typeof(FileResponse[]))]
[JsonSerializable(typeof(Message[]))]
[JsonSerializable(typeof(NotificationMessage[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext {}
