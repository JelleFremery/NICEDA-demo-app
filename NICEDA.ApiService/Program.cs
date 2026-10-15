using NICEDA.ApiService.Endpoints;
using NICEDA.ApiService.Agent;
using NICEDA.ApiService.Rag;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations. The shared standard
// resilience handler has a 10-second attempt timeout, too short for Foundry
// embedding calls during an agent run; outbound clients register their own.
builder.AddServiceDefaults(addStandardResilienceHandler: false);

// Real data stores (NICEDA-PRACTICE-1: see docs/data-inventory.md for why
// only these two are real containers while CRM/SharePoint are mocked).
builder.AddSqlServerClient("niceda");

// Documents throughout this project are modelled as System.Text.Json
// JsonObject (so the "before" endpoints can pass through whatever
// inconsistent shape is on disk/in Cosmos unchanged). The Cosmos SDK
// defaults to a Newtonsoft.Json serializer, which cannot serialize
// JsonObject's internal Parent back-reference. Switch the client to
// System.Text.Json so JsonObject documents round-trip correctly.
builder.AddAzureCosmosClient("cosmos", configureClientOptions: options =>
{
    options.UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions();
    options.ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway;
    options.RequestTimeout = TimeSpan.FromSeconds(15);
});

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<NICEDA.ApiService.Crm.CrmRepository>();
builder.Services.AddSingleton<NICEDA.ApiService.Legal.LegalRepository>();
builder.Services.AddSingleton<NICEDA.ApiService.Sql.RawSqlSeeder>();
builder.Services.AddSingleton<NICEDA.ApiService.Sql.OptimizedSqlSeeder>();
builder.Services.AddSingleton<NICEDA.ApiService.SupportDocs.RawSupportSeeder>();
builder.Services.AddSingleton<NICEDA.ApiService.SupportDocs.OptimizedSupportSeeder>();
builder.Services.AddHostedService<NICEDA.ApiService.SeedDataHostedService>();
builder.Services.AddNicedaRagAndFeedback();
builder.Services.AddNicedaAgentServices();
builder.Services.AddScoped<NICEDA.ApiService.DataLifecycle.CustomerDataLifecycle>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDataExplorerEndpoints();
app.MapAgentChatEndpoints();

app.MapDefaultEndpoints();

app.Run();
