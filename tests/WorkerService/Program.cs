using EventBus.InMemory.Tests.Implementations;
using Softoverse.EventBus.InMemory;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundWorkerService>();

builder.Services.AddEventBus(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddSingleton(new EventTracker());

var host = builder.Build();
await host.RunAsync();