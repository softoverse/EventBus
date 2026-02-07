namespace EventBus.InMemory.Tests;

public class CustomEventProcessorWithGeneralTest()
    : BaseTest(configFileName: "appSettings.General.json",
               useDefaultEventProcessor: false)
{
}
