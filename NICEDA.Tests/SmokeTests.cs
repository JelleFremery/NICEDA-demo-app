namespace NICEDA.Tests;

/// <summary>
/// Smoke test proving the test project and the solution-wide
/// "warnings are errors" build policy are wired up correctly before any
/// feature slice is added. See docs/demo-briefing.md §8/§9, step 1.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void TestProjectIsWired()
    {
        Assert.True(true);
    }
}
