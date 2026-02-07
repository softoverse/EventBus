using System.Reflection;
using SampleCore;
using Softoverse.EventBus.InMemory;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundWorkerService>();

List<Assembly> assemblies =
[
    typeof(Program).Assembly,
    typeof(TestEvent).Assembly
];

builder.Services.AddEventBus(builder.Configuration, assemblies);
builder.Services.AddSingleton(new EventTracker());

var host = builder.Build();
await host.RunAsync();