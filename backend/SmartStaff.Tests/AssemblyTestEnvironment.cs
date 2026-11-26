using SmartStaff.Tests;

[SetUpFixture]
public sealed class AssemblyTestEnvironment
{
    [OneTimeSetUp]
    public async Task Prepare()
    {
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", "smartstaff-test-signing-key-not-for-production");
        Environment.SetEnvironmentVariable("AUTH_RATE_LIMIT_PER_MINUTE", "1000");
        await TestDatabase.PrepareAsync();
    }
}
