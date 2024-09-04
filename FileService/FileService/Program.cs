using Dapr.Client;
using FileService.Models;
using FileService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spin.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpinHttpWorld.wit.imports.wasi.http.v0_2_0;

namespace SpinHttpWorld.wit.exports.wasi.http.v0_2_0;

public class IncomingHandlerImpl : IIncomingHandler
{
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

            options.UseGrpcEndpoint("http://127.0.0.1:53501");
            // force the runtime to use the HTTP client handler
            // NOTE: this can be removed once System.Net.Sockets support is available for WASI
            options.UseGrpcChannelOptions(new Grpc.Net.Client.GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler()
            });
        });

        builder.Services.AddTransient<IFileService, FileService.Services.FileService>();

        var app = builder.Build();

        app.UseRouting();
        app.MapHealthChecks("/healthz");
        app.MapOpenApi();

        app.UseCloudEvents();

        app.MapGet("/api/v1.0/File/{fileName}", async context =>
        {
            app.Logger.LogInformation("fetching file service");
            var fileService = context.RequestServices.GetRequiredService<IFileService>();
            app.Logger.LogInformation("fetched file service");
            var fileName = context.Request.RouteValues["fileName"] as string;
            app.Logger.LogInformation("fetching file {Filename}", fileName);
            var fileRequest = await fileService.Get(fileName!);
            app.Logger.LogInformation("fetched file {Filename}", fileName);
            // NOTE(bacongobbler): necessary to unescape certain characters
            // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft?pivots=dotnet-9-0#minimal-character-escaping
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = AppJsonSerializerContext.Default,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            await context.Response.WriteAsJsonAsync(fileRequest, jsonSerializerOptions);
        });

        app.MapPost("/api/v1.0/File", async context =>
        {
            app.Logger.LogInformation("fetching file service");
            var fileService = context.RequestServices.GetRequiredService<IFileService>();
            app.Logger.LogInformation("fetched file service");

            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            if (context.Request.ContentLength == 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var fileRequest = await context.Request.ReadFromJsonAsync<FileRequest>();
            if (fileRequest is null || string.IsNullOrEmpty(fileRequest.Base64))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            app.Logger.LogInformation("saving file {FileRequest}", fileRequest);
            await fileService.Save(fileRequest);
            app.Logger.LogInformation("saved file {FileRequest}", fileRequest);
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
[JsonSerializable(typeof(FileRequest[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext {}
