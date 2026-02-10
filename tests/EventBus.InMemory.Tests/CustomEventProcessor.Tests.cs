namespace EventBus.InMemory.Tests;

public class CustomEventProcessorTests()
    : BaseTest(configFileName: "appSettings.json", useDefaultEventProcessor: false);
