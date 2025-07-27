using System.Reflection;
using hello_world_assignment;
using hello_world_assignment.Components;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

var otelCollectorHostname = Environment.GetEnvironmentVariable("OTEL_COLLECTOR_HOSTNAME") ?? "localhost";
var otelGrpcEndpoint = "http://" + otelCollectorHostname + ":4317";
var otelHttpEndpoint = "http://" + otelCollectorHostname + ":4318";

Console.WriteLine("Got env variables: ");
Console.WriteLine($"otelCollectorHostname: {otelCollectorHostname}");
Console.WriteLine($"otelGrpcEndpoint: {otelGrpcEndpoint}");
Console.WriteLine($"otelHttpEndpoint: {otelHttpEndpoint}");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add custom instrumentation service
builder.Services.AddSingleton<HelloWorldMetrics>();


//--------------------------\\
// Begin OTLP configuration \\
//--------------------------\\

builder.Host.UseSerilog((context, logging) =>
{
    //---------------------------------------\\
    // General Serilog logging configuration \\
    //---------------------------------------\\
    logging
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "HellWorldApp")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version.ToString())
        
        .WriteTo.Console()
        
        //------------------------------------\\
        // Serilog OTLP logging configuration \\
        //------------------------------------\\
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otelHttpEndpoint;
            options.Protocol = OtlpProtocol.HttpProtobuf;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                { "service.namespace", "hello-world" },
                { "service.name", "hello-world-app" }
            };
        });
});


//-----------------------------\\
// OTLP .NET SDK configuration \\
//-----------------------------\\

// The below configuration uses the .NET open telemetry SDK
// and the OpenTelemetry.Extension.Hosting package.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddAttributes(new Dictionary<string, object>
        {
            { "service.namespace", "hello-world" },
            { "service.name", "hello-world-app" }
        });
        resource.AddService("HelloWorldApp");
        resource.AddTelemetrySdk();
    })
    
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("HelloWorldActivitySource")
            
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otelGrpcEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
        });
    })
    
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()

            // Built-in metrics
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNet.Server.Kestrel")
            
            
            // App metrics
            .AddMeter("HelloWorldApp")
            

            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otelGrpcEndpoint);
                opts.Protocol = OtlpExportProtocol.Grpc;
            });
    });

//------------------------\\
// End OTLP configuration \\
//------------------------\\




var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();