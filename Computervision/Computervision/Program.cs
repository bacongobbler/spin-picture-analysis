using Computervision.Daos;
using Computervision.Models;
using Computervision.Services;
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
            options.UseHttpEndpoint("http://127.0.0.1:3500");

            options.UseGrpcEndpoint("http://127.0.0.1:53500");
            // force the runtime to use the HTTP client handler
            // NOTE: this can be removed once System.Net.Sockets support is available for WASI
            options.UseGrpcChannelOptions(new Grpc.Net.Client.GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler()
            });
        });
        builder.Services.AddTransient<IFileDao, FileDao>();
        builder.Services.AddTransient<IComputervisionService, ComputervisionService>();
        builder.Services.AddTransient<IAnalysisService, AnalysisService>();

        var app = builder.Build();

        app.UseRouting();
        app.MapHealthChecks("/healthz");
        app.MapOpenApi();

        app.UseCloudEvents();

        app.MapPost("/api/v1.0/Computervision", async context =>
        {
            var computervisionService = context.RequestServices.GetRequiredService<IComputervisionService>();
            if (computervisionService is null)
            {
                app.Logger.LogWarning("computer vision service is unavailable");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
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
            var message = await context.Request.ReadFromJsonAsync<Message>();
            if (message is null || string.IsNullOrEmpty(message.FileReference))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            app.Logger.LogInformation("processing image {FileReference}", message.FileReference);
            await computervisionService.ProcessImage(message.FileReference);
            app.Logger.LogInformation("processed image {FileReference}", message.FileReference);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        })
        .WithTopic("messagebus", "message-received");

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
[JsonSerializable(typeof(Dictionary<string, string>[]))]
public partial class AppJsonSerializerContext : JsonSerializerContext {}
