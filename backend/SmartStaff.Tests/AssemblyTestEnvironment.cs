[SetUpFixture]
public sealed class AssemblyTestEnvironment
{
    [OneTimeSetUp]
    public void Configure()
    {
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", "smartstaff-test-signing-key-not-for-production");
        Environment.SetEnvironmentVariable("AUTH_RATE_LIMIT_PER_MINUTE", "1000");
    }
}
