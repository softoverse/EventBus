using SampleCore;

namespace EventBus.InMemory.Tests;

public class InMemoryEventProcessorTest()
    : BaseTest(configFileName: "appSettings.Channel.json",
               useDefaultEventProcessor: true)
{
}
