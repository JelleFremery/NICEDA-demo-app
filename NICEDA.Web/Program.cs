using NICEDA.Web;
using NICEDA.Web.Components;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();
builder.Services.AddScoped<DemoState>();

// The global standard handler disables retries for unsafe methods. Give only
// the agent chat client more time for model/tool orchestration.
builder.Services.Configure<HttpStandardResilienceOptions>(nameof(DemoApiClient), options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
});
builder.Services.AddHttpClient<DemoApiClient>(client =>
{
    client.BaseAddress = new("https+http://apiservice");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient<DataExplorerApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
