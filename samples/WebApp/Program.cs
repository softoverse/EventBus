using System.Reflection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SampleCore;
using Softoverse.EventBus.InMemory;
using WebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

List<Assembly> assemblies = [
    typeof(Program).Assembly, 
    typeof(TestScheduledHandler).Assembly
];

if (Convert.ToBoolean(builder.Configuration["UseCustomEventProcessor"]))
{
    builder.Services.AddEventBus<CustomEventProcessor>(builder.Configuration, assemblies);
}
else
{
    builder.Services.AddEventBus(builder.Configuration, assemblies);
}

builder.Services.AddSingleton(new EventTracker());

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("EventBus.WebApp.Sample")
        .AddTelemetrySdk())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // Add EventBus tracing - this enables distributed tracing for all event bus operations
        .AddSource("Softoverse.EventBus.InMemory")
        // Export to console for demo purposes (replace with OTLP/Seq/Jaeger in production)
        .AddConsoleExporter()
        // Uncomment below to export to Seq (requires Seq running on localhost:5341)
        // .AddOtlpExporter(options =>
        // {
        //     options.Endpoint = new Uri("http://localhost:5341/ingest/otlp/v1/traces");
        //     options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        // })
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapSwagger();
    app.UseSwaggerUI(options =>
    {
        options.EnableTryItOutByDefault();
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();

app.MapEndpoints();

await app.RunAsync();
