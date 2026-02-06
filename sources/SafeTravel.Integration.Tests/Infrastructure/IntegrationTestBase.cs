namespace SafeTravel.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests that use the IntegrationTestWebApplicationFactory.
/// Provides convenient access to the factory and HTTP client.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IntegrationTestWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(IntegrationTestWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        Factory.WireMockServer.ResetMappings();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Collection definition for integration tests.
/// Ensures the factory is shared and container lifecycle is managed properly.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
}
