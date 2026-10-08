using NICEDA.ApiService.Sql;

namespace NICEDA.Tests;

public class FollowUpValidationTests
{
    [Theory]
    [InlineData("", "Check renewal", "jane")]
    [InlineData("betabit", "", "jane")]
    [InlineData("betabit", "Check renewal", "")]
    public void EmptyCommandFieldsAreRejected(string customer, string reason, string creator) =>
        Assert.Throws<ArgumentException>(() => FollowUpTaskCommands.Validate(
            new(customer, reason, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), creator)));

    [Fact]
    public void PastAndOversizeInputsAreRejected()
    {
        var valid = new CreateFollowUpTaskCommand("betabit", "Check renewal",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "jane");
        FollowUpTaskCommands.Validate(valid);
        Assert.Throws<ArgumentException>(() => FollowUpTaskCommands.Validate(valid with { Reason = new string('x', 501) }));
        Assert.Throws<ArgumentException>(() => FollowUpTaskCommands.Validate(valid with { DueDate = valid.DueDate.AddDays(-10) }));
    }
}
