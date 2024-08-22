using FileService.Models;
using FileService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spin.Http;
using SpinHttpWorld.wit.imports.wasi.http.v0_2_0;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
        builder.Services.AddDaprClient(options =>
        {
            // https://github.com/dapr/dotnet-sdk/issues/1097#issuecomment-1960876594
            var op = new JsonSerializerOptions();
            op.TypeInfoResolverChain.Clear();
            op.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
            options.UseJsonSerializationOptions(op);

            // We need to tell dapr what endpoint the sidecar is listening on because we cannot read environment variables like $DAPR_HTTP_ENDPOINT.
            // https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-client/dotnet-daprclient-usage/
            options.UseHttpEndpoint("http://127.0.0.1:3501");
        });
        builder.Services.AddTransient<IFileService, FileService.Services.FileService>();

        var app = builder.Build();

        app.UseRouting();
        app.MapHealthChecks("/healthz");
        app.MapOpenApi();

        app.UseCloudEvents();

        app.MapGet("/api/v1.0/File/{fileName}", async context =>
        {
            var fileName = context.Request.RouteValues["fileName"] as string;
            app.Logger.LogInformation("fetching file {Filename}", fileName);
            var fileService = context.RequestServices.GetRequiredService<IFileService>();
            var fileRequest = await fileService.Get(fileName!);
            app.Logger.LogInformation("fetched file {Filename}", fileName);
            await context.Response.WriteAsJsonAsync(fileRequest);
        });

        // app.MapPost("/api/v1.0/File", async context =>
        // {
        //     var fileService = context.RequestServices.GetRequiredService<IFileService>();
        //     var fileReference = "foo";
        //     app.Logger.LogInformation("processing image {FileReference}", fileReference);
        //     await ComputervisionService.ProcessImage(fileReference);
        //     app.Logger.LogInformation("processed image {FileReference}", fileReference);
        //     context.Response.StatusCode = StatusCodes.Status204NoContent;
        // });

        Func<Task> task = async () =>
        {
            await app.StartAsync();
            await WasiHttpServer.HandleRequestAsync(request, response);
        };

        RequestHandler.Run(task());
    }
}

[JsonSerializable(typeof(FileResponse[]))]
[JsonSerializable(typeof(FileRequest[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext {}
