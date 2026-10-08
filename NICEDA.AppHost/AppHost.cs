var builder = DistributedApplication.CreateBuilder(args);
builder.AddDockerComposeEnvironment("compose");
var foundryKey = builder.AddParameter("foundry-api-key",
    builder.Configuration["Foundry:ApiKey"] ?? string.Empty, secret: true);

// NICEDA-PRACTICE-1: the SQL Server and Cosmos DB containers below are the
// two "real" data stores from the data inventory (docs/data-inventory.md).
// CRM and SharePoint are intentionally mocked (in-memory seed data) per the
// demo plan — only the stores that genuinely need agentic-access treatment
// run as real containers.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("niceda");

// A container resource is intentional: RunAsEmulator on an Azure resource
// switches back to cloud provisioning when publishing. This demo must keep
// the real emulator in Docker both locally and in generated Compose output.
const string emulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
// This is the public Cosmos emulator key, not a production credential.
var cosmos = builder.AddContainer("cosmos", "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator", "vnext-latest")
    .WithEnvironment("PROTOCOL", "https")
    .WithHttpsEndpoint(targetPort: 8081, name: "gateway")
    .WithHttpEndpoint(targetPort: 8080, name: "readiness")
    .WithHttpHealthCheck("/ready", endpointName: "readiness")
    .WithVolume("niceda-cosmos-demo-data", "/data")
    .WithLifetime(ContainerLifetime.Persistent);
var cosmosConnection = ReferenceExpression.Create(
    $"AccountEndpoint={cosmos.GetEndpoint("gateway")};AccountKey={emulatorKey};DisableServerCertificateValidation=True");

var mcpService = builder.AddProject<Projects.NICEDA_McpServer>("mcpserver")
    .WithHttpHealthCheck("/health");

var apiService = builder.AddProject<Projects.NICEDA_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(sql)
    .WaitFor(sql)
    .WithEnvironment("ConnectionStrings__cosmos", cosmosConnection)
    .WaitFor(cosmos)
    .WithReference(mcpService)
    .WithEnvironment("Mcp__Endpoint", mcpService.GetEndpoint("http"))
    .WithEnvironment("Foundry__Endpoint", builder.Configuration["Foundry:Endpoint"]
        ?? "https://bblc-we-ai-foundry.services.ai.azure.com/")
    .WithEnvironment("Foundry__ChatModel", builder.Configuration["Foundry:ChatModel"] ?? "gpt-6.1-sol")
    .WithEnvironment("Foundry__EmbeddingModel", builder.Configuration["Foundry:EmbeddingModel"] ?? "text-embedding-3-small")
    .WithEnvironment("Foundry__ApiKey", foundryKey);

// The API starts without connecting to MCP; only a chat action needs it.
// MCP waits for the seeded API, so this does not create a startup wait cycle.
mcpService.WithReference(apiService)
    .WithEnvironment("ApiService__BaseUrl", apiService.GetEndpoint("http"))
    .WaitFor(apiService);

builder.AddProject<Projects.NICEDA_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WaitFor(mcpService);

builder.Build().Run();
