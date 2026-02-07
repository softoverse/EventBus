using SampleCore;

namespace EventBus.InMemory.Tests;

public class DefaultEventProcessorTest()
    : BaseTest(configFileName: "appSettings.Channel.json",
               useDefaultEventProcessor: true)
{
}
