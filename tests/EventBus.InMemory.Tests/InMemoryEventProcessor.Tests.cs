namespace EventBus.InMemory.Tests;

public class InMemoryEventProcessorTest()
    : BaseTest(configFileName: "appSettings.json", useDefaultEventProcessor: true);
