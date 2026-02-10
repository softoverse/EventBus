using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleCore;
using Softoverse.EventBus.InMemory;
using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests.Core;

public abstract class TestContext(
    bool useDefaultEventProcessor = false,
    string configFileName = TestContext.DefaultConfigFileName)
    : IAsyncLifetime
{
    protected const string DefaultConfigFileName = "appSettings.json";
    protected const int DelayMilliseconds = 1000 * 3;

    private IHost? _host;
    private IConfiguration Configuration;

    protected IServiceProvider Services;
    protected IEventBus EventBus;
    protected EventTracker EventTracker;

    public async Task InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();

        // Load appSettings.json into configuration
        builder.Configuration.AddJsonFile(configFileName, optional: false, reloadOnChange: true);
        Configuration = builder.Configuration;

        List<Assembly> assemblies =
        [
            typeof(BaseTests).Assembly,
            typeof(TestEvent).Assembly
        ];

        if (useDefaultEventProcessor)
        {
            builder.Services.AddEventBus(Configuration, assemblies);
        }
        else
        {
            builder.Services.AddEventBus<CustomEventProcessor>(Configuration, assemblies);
        }
        builder.Services.AddSingleton(new EventTracker());

        _host = builder.Build();

        // This will start the host and ensure that the event bus is ready to process events. Eventually it will be stopped after execution of the tests.
        await _host.StartAsync();

        // This will keep the host running until it's stopped, allowing events to be processed. But it will keep the host running indefinitely,
        // await _host.RunAsync();

        await using var scope = _host.Services.CreateAsyncScope(); // Ensure the scope is created before resolving services
        Services = scope.ServiceProvider;

        EventBus = Services.GetRequiredService<IEventBus>();
        EventTracker = Services.GetRequiredService<EventTracker>();

        // Clear tracking before each test
        EventTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        if (_host != null!)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    protected async Task WaitForProcessingAsync(int count = 0)
    {
        int delayMs = (DelayMilliseconds * Math.Abs(count)) + 100; // Add extra time to ensure processing is complete
        // Wait for a reasonable time to allow events to be processed
        await Task.Delay(delayMs);
    }
}
