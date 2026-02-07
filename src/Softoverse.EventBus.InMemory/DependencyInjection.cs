using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Infrastructure.Channels;
using Softoverse.EventBus.InMemory.Infrastructure.General;
using Softoverse.EventBus.InMemory.Infrastructure.Processors;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory;

public static class DependencyInjection
{
    public static bool UsingDefaultEventProcessor = false;
    
    public static IServiceCollection AddEventBus<TEventProcessor>(this IServiceCollection services, IConfiguration configuration, params List<Assembly> assemblies)
        where TEventProcessor : class, IEventProcessor
    {
        if (typeof(TEventProcessor) == typeof(DefaultEventProcessor))
        {
            throw new Exception("Use AddEventBus overload without type parameter to use DefaultEventProcessor");
        }
        
        UsingDefaultEventProcessor = false;
        string eventBusType = configuration[BuildConstants.EventBusTypeConfigPath] ?? BuildConstants.Channel;

        services.AddEventBusSettings(configuration);
        services.AddScoped<IEventProcessor, TEventProcessor>();

        if (string.Equals(eventBusType, BuildConstants.Channel, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<ChannelEventsPublishingHostedService>();
            services.AddHostedService<ChannelEventsSchedulingHostedService>();
            services.AddSingleton<EventChannelProvider>();
            services.AddScoped<ChannelEventBus>();
            services.AddScoped<IEventBus>(sp => sp.GetRequiredService<ChannelEventBus>());
        }
        else
        {
            services.AddScoped<IEventBus, GeneralEventBus>();
        }

        // Register all IEventHandler<T> implementations from application assemblies
        RegisterHandlersFromAssembly(services, assemblies);
        return services;
    }

    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration, params List<Assembly> assemblies)
    {
        UsingDefaultEventProcessor = true;
        string eventBusType = configuration[BuildConstants.EventBusTypeConfigPath] ?? BuildConstants.Channel;

        services.AddEventBusSettings(configuration);
        services.AddScoped<IEventProcessor, DefaultEventProcessor>();

        services.AddSingleton<ScheduledEventStore>();
        services.AddHostedService<ScheduledEventProcessingHostedService>();

        if (string.Equals(eventBusType, BuildConstants.Channel, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<ChannelEventsPublishingHostedService>();
            services.AddHostedService<ChannelEventsSchedulingHostedService>();
            services.AddSingleton<EventChannelProvider>();
            services.AddScoped<ChannelEventBus>();
            services.AddScoped<IEventBus>(sp => sp.GetRequiredService<ChannelEventBus>());
        }
        else
        {
            services.AddScoped<IEventBus, GeneralEventBus>();
        }

        // Register all IEventHandler<T> implementations from application assemblies
        RegisterHandlersFromAssembly(services, assemblies);
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
                        services.AddScoped(implType);
                    }
                }
            }
            

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
                        services.AddScoped(gi, implType);
                        services.AddScoped(implType);
                    }
                }
            }
        }
    }
}
