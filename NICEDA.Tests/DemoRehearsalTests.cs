using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NICEDA.ApiService.SupportDocs;
using NICEDA.Web;
using NICEDA.Web.Components.Pages;

namespace NICEDA.Tests;

public sealed class DemoRehearsalTests
{
    [Fact]
    public void CodeTourBookmarksHaveUniqueAnchors()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools", "demo-bookmarks.json")));
        Assert.Equal(13, manifest.RootElement.GetArrayLength());
        foreach (var bookmark in manifest.RootElement.EnumerateArray())
        {
            var path = Path.Combine(root, bookmark.GetProperty("file").GetString()!
                .Replace('\\', Path.DirectorySeparatorChar));
            var anchor = bookmark.GetProperty("anchor").GetString()!;
            Assert.True(File.Exists(path), $"Missing bookmark file: {path}");
            Assert.Single(File.ReadLines(path), line => line.Contains(anchor, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ContosoWalkthroughHasSecurityEvidenceAndTraceableConceptChunks()
    {
        var raw = RawSupportSeeder.SeedDocuments().Single(document => document["id"]!.GetValue<string>() == "support-005");
        Assert.Equal("contoso", raw["customerId"]!.GetValue<string>());
        Assert.Contains("dedicated Contoso escalation contact", raw["fullText"]!.GetValue<string>());

        var chunks = OptimizedSupportSeeder.SeedDocuments()
            .Where(document => document["sourceId"]!.GetValue<string>() == "support-005").ToArray();
        Assert.Equal(
            ["optimized-sup-005a", "optimized-sup-005b", "optimized-sup-005c"],
            chunks.Select(document => document["id"]!.GetValue<string>()));
        Assert.Equal(
            ["security-finding-access-controls", "security-finding-data-residency", "security-finding-incident-response"],
            chunks.Select(document => document["concept"]!.GetValue<string>()));
        Assert.All(chunks, document =>
        {
            Assert.Equal("contoso", document["customerId"]!.GetValue<string>());
            Assert.Equal("NA", document["region"]!.GetValue<string>());
            Assert.Equal("Confidential", document["classification"]!.GetValue<string>());
            Assert.Equal(730, document["retentionDays"]!.GetValue<int>());
        });
        Assert.Contains("dedicated Contoso escalation contact", chunks[2]["summary"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task ChatSamplesUseAuthorizedEvidenceAndExplicitFutureDueDate(string mode)
    {
        var demo = new DemoState();
        demo.SetMode(mode);
        using var client = new HttpClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(demo);
        services.AddSingleton(new DemoApiClient(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Chat>();
            var html = System.Net.WebUtility.HtmlDecode(component.ToHtmlString());
            Assert.Contains("Summarize Techorama's data export incident with sources.", html);
            Assert.DoesNotContain("onboarding blockers", html);
            var match = System.Text.RegularExpressions.Regex.Match(html, @"Due date: (\d{4}-\d{2}-\d{2})");
            Assert.True(match.Success);
            Assert.True(DateOnly.ParseExact(match.Groups[1].Value, "yyyy-MM-dd") > DateOnly.FromDateTime(DateTime.UtcNow));
            Assert.Equal(mode == "before", html.Contains("capability is unavailable", StringComparison.Ordinal)
                || html.Contains("Before mode does not provide", StringComparison.Ordinal));
            Assert.Equal("jane", demo.UserId);
        });
    }
}
