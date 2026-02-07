namespace EventBus.InMemory.Tests;

public class CustomEventProcessorWithChannelTest()
    : BaseTest(configFileName: "appSettings.Channel.json",
               useDefaultEventProcessor: false)
{
}
