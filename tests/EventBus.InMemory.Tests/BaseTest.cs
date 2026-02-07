using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleCore;
using Softoverse.EventBus.InMemory;
using Softoverse.EventBus.InMemory.Abstractions;

namespace EventBus.InMemory.Tests;

public abstract class BaseTest(string configFileName, bool useDefaultEventProcessor = false) : IAsyncLifetime
{
    private IHost _host;
    private IConfiguration _configuration;

    protected IEventBus _eventBus;
    protected EventTracker _eventTracker;
    protected readonly int _delayMilliseconds = 1000 * 3;

    public async Task InitializeAsync()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        // Load appSettings.json into configuration
        hostBuilder.Configuration.AddJsonFile(configFileName, optional: false, reloadOnChange: true);
        _configuration = hostBuilder.Configuration;

        if (useDefaultEventProcessor)
        {
            hostBuilder.Services.AddEventBus(hostBuilder.Configuration, typeof(BaseTest).Assembly);
        }
        else
        {
            hostBuilder.Services.AddEventBus<CustomEventProcessor>(hostBuilder.Configuration, typeof(BaseTest).Assembly);
            // hostBuilder.Services.AddScoped<IRequestHandler<TestRequest, bool>, TestRequestHandler>();
            // hostBuilder.Services.AddScoped<IRequestHandler<TestRequest, bool>, TestRequestHandler>();
        }
        hostBuilder.Services.AddSingleton(new EventTracker());

        _host = hostBuilder.Build();
        await _host.StartAsync();

        await using var scope = _host.Services.CreateAsyncScope(); // Ensure the scope is created before resolving services
        _eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        _eventTracker = scope.ServiceProvider.GetRequiredService<EventTracker>();

        // Clear tracking before each test
        _eventTracker.Clear();
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
        int delayMs = (_delayMilliseconds * Math.Abs(count)) + 1000; // Add extra time to ensure processing is complete
        // Wait for a reasonable time to allow events to be processed
        await Task.Delay(delayMs);
    }
}
