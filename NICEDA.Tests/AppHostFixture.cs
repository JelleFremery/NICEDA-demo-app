namespace NICEDA.Tests;

// Docker checks run against an already-started AppHost; ordinary tests never
// cold-start an emulator or wait on distributed-application teardown.
public sealed class AppHostFixture : IDisposable
{
    public HttpClient ApiClient { get; } = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("NICEDA_TEST_API_URL") ?? "http://localhost:5000"),
        Timeout = TimeSpan.FromSeconds(20),
    };

    public Task<string?> GetSqlConnectionStringAsync() =>
        Task.FromResult(Environment.GetEnvironmentVariable("NICEDA_TEST_SQL"));

    public Task<string?> GetCosmosConnectionStringAsync() =>
        Task.FromResult(Environment.GetEnvironmentVariable("NICEDA_TEST_COSMOS"));

    public void Dispose() => ApiClient.Dispose();
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NICEDA_TEST_API_URL") is null)
        {
            Skip = "Opt-in Docker check: start AppHost and set NICEDA_TEST_API_URL, NICEDA_TEST_SQL and NICEDA_TEST_COSMOS.";
        }
    }
}

[CollectionDefinition(nameof(AppHostCollection))]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>;
