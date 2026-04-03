namespace XianYuLauncher.Core.Services;

public static class AgentOperationPollingDefaults
{
    public const int MinSleepSeconds = 1;

    public const int DefaultSuggestedPollDelaySeconds = 10;

    public const int MaxSleepSeconds = 30;

    public static int NormalizeSleepSeconds(int? seconds)
    {
        return Math.Clamp(
            seconds ?? DefaultSuggestedPollDelaySeconds,
            MinSleepSeconds,
            MaxSleepSeconds);
    }
}