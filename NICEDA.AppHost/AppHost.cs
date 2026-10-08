var builder = DistributedApplication.CreateBuilder(args);

// NICEDA-PRACTICE-1: the SQL Server and Cosmos DB containers below are the
// two "real" data stores from the data inventory (docs/data-inventory.md).
// CRM and SharePoint are intentionally mocked (in-memory seed data) per the
// demo plan — only the stores that genuinely need agentic-access treatment
// run as real containers.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .AddDatabase("niceda");

var cosmos = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator(emulator => emulator.WithDataVolume());
var cosmosDb = cosmos.AddCosmosDatabase("niceda-cosmos", databaseName: "niceda");
cosmosDb.AddContainer("raw-support", "/customerId");
cosmosDb.AddContainer("optimized-support", "/customerId");

var apiService = builder.AddProject<Projects.NICEDA_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(sql)
    .WaitFor(sql)
    .WithReference(cosmos)
    .WaitFor(cosmos);

builder.AddProject<Projects.NICEDA_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
