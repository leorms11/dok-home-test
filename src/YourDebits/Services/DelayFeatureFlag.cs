namespace YourDebits.Services;

public class DelayFeatureFlag
{
    public bool IsEnabled { get; set; } = false;
    public int DelayMs { get; set; } = 0;
}