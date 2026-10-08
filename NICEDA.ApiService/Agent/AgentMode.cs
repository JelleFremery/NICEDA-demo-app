namespace NICEDA.ApiService.Agent;

public enum AgentMode
{
    Before,
    After,
}

public static class AgentModes
{
    public static bool TryParse(string? value, out AgentMode mode) =>
        Enum.TryParse(value, ignoreCase: true, out mode) && Enum.IsDefined(mode);
}
