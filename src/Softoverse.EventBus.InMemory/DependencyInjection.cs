using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Softoverse.EventBus.InMemory.Abstractions;
using Softoverse.EventBus.InMemory.Channels;
using Softoverse.EventBus.InMemory.General;
using Softoverse.EventBus.InMemory.Models.Settings;

namespace Softoverse.EventBus.InMemory;

public static class DependencyInjection
{
    public static IServiceCollection AddEventBus<TEventProcessor>(this IServiceCollection services, IConfiguration configuration, params List<Assembly> assemblies)
        where TEventProcessor : class, IEventProcessor
    {
        string eventBusType = configuration[BuildConstants.EventBusTypeConfigPath] ?? BuildConstants.Channel;

        services.AddEventBusSettings(configuration);
        services.AddScoped<IEventProcessor, TEventProcessor>();

        if (string.Equals(eventBusType, BuildConstants.Channel, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<ChannelEventsHostedService>();
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
        var marker = typeof(IEventHandler);

        assemblies = assemblies ?? [];

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && marker.IsAssignableFrom(t));

            foreach (var implType in types)
            {
                // Register as non-generic IEventHandler to allow simple resolution
                services.AddScoped(marker, implType);

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
        }
    }
}
