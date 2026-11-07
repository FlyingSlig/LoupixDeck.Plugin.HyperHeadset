namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>Shared constants for the plugin's commands and settings.</summary>
internal static class PluginConstants
{
    /// <summary>Group/category shown in the command picker.</summary>
    public const string Group = "HyperX Headset";

    /// <summary>Executable name used when the user has not configured a path.</summary>
    public const string DefaultExecutable = "hyper_headset_cli";

    /// <summary>Default delay between automatic headset reads, in seconds.</summary>
    public const long DefaultPollSeconds = 30;

    /// <summary>Stable command identifier — never change after release.</summary>
    public const string BatteryCommandName = "HyperHeadset.Battery";

    // Material Design Icons glyphs (single code points).
    public const string HeadsetGlyph = "\U000F02CE";
    public const string BatteryGlyph = "\U000F0079";
}
