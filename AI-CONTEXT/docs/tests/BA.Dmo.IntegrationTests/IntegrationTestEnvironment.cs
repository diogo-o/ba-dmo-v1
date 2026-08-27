using System.Runtime.CompilerServices;

namespace BA.Dmo.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    [ModuleInitializer]
    internal static void Configure()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Logging__EventLog__LogLevel__Default", "None");
    }
}
