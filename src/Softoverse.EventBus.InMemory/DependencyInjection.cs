using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Infrastructure;
using Softoverse.EventBus.InMemory.Infrastructure.Services;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory;

public static class DependencyInjection
{
    internal static bool UsingDefaultEventProcessor = false;
    
    public static IServiceCollection AddEventBus<TEventProcessor>(this IServiceCollection services, IConfiguration configuration, params List<Assembly> assemblies)
        where TEventProcessor : class, IEventProcessor
    {        
        UsingDefaultEventProcessor = false;

        services.AddEventBusSettings(configuration);
        services.AddScoped<IEventProcessor, TEventProcessor>();

        services.AddHostedService<EventPublishingHostedService>();
        services.AddHostedService<EventSchedulingHostedService>();
        services.AddSingleton<EventChannelProvider>();
        services.AddScoped<ChannelEventBus>();
        services.AddScoped<IEventBus>(sp => sp.GetRequiredService<ChannelEventBus>());

        // Register all IEventHandler<T> implementations from application assemblies
        RegisterHandlersFromAssembly(services, assemblies);
        return services;
    }

    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration, params List<Assembly> assemblies)
    {
        services.AddEventBus<InMemoryEventProcessor>(configuration, assemblies);
        UsingDefaultEventProcessor = true;

        services.AddSingleton<ScheduledEventStore>();
        services.AddHostedService<ScheduledEventProcessingHostedService>();

        return services;
    }

    public static EventBusSettings GetEventBusSettings(this IConfiguration configuration)
    {
        var settings = new EventBusSettings();
        configuration.GetSection(EventBusSettings.SectionName).Bind(settings);
        return settings;
    }

    private static IServiceCollection AddEventBusSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetEventBusSettings();
        services.AddSingleton(settings);
        return services;
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, params IList<Assembly> assemblies)
    {
        var eventHandlerMarker = typeof(IEventHandler);
        var requestHandlerMarker = typeof(IRequestHandler);

        assemblies = assemblies ?? [];

        foreach (var assembly in assemblies)
        {
            var eventHandlerTypes = assembly.GetTypes().Where(t => t is
                                                  {
                                                      IsClass   : true,
                                                      IsAbstract: false
                                                  } && eventHandlerMarker.IsAssignableFrom(t));

            foreach (var implType in eventHandlerTypes)
            {
                // Register as non-generic IEventHandler to allow simple resolution
                services.AddScoped(eventHandlerMarker, implType);

                {
                    var typeOfGenericIEnventHandler = typeof(IEventHandler<>);

                    // Also register its generic interfaces for flexibility
                    var genericInterfaces = implType.GetInterfaces()
                                                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeOfGenericIEnventHandler);
                    foreach (var gi in genericInterfaces)
                    {
                        services.AddScoped(gi, implType);
                        //services.AddScoped(implType);
                    }
                }
            }
            

            var genericRequestHandlerTypes = new HashSet<Type>();
            var requestHandlerTypes = assembly.GetTypes().Where(t => t is
                                                  {
                                                      IsClass   : true,
                                                      IsAbstract: false
                                                  } && requestHandlerMarker.IsAssignableFrom(t));

            foreach (var implType in requestHandlerTypes)
            {
                // Register as non-generic IRequestHandler to allow simple resolution
                services.AddScoped(requestHandlerMarker, implType);

                {
                    var typeOfGenericIRequestHandler = typeof(IRequestHandler<,>);

                    // Also register its generic interfaces for flexibility
                    var genericInterfaces = implType.GetInterfaces()
                                                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeOfGenericIRequestHandler);
                    foreach (var gi in genericInterfaces)
                    {
                        if (genericRequestHandlerTypes.TryGetValue(gi, out _))
                        {
                            throw new Exception($"Multiple implementations of {gi} found. Please ensure only one implementation exists for each IRequestHandler<TRequest, TResponse>.");
                        }
                        genericRequestHandlerTypes.Add(gi);
                        services.AddScoped(gi, implType);
                        //services.AddScoped(implType);
                    }
                }
            }
        }
    }
}
