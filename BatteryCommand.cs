using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>
/// Touch-button command that shows the headset's battery level as a gauge and its charging
/// status as a caption. The host polls <see cref="GetValue"/>; the underlying read runs on the
/// monitor's background loop, so this only returns a cached snapshot.
/// </summary>
internal sealed class BatteryCommand(HeadsetMonitor monitor) : IValueDisplayCommand
{
    public CommandDescriptor Descriptor { get; } = new()
    {
        CommandName = PluginConstants.BatteryCommandName,
        DisplayName = "Battery",
        Group = PluginConstants.Group,
        Icon = PluginConstants.BatteryGlyph,
        Description = "Battery level and charging status of the HyperX headset.",
        ButtonLayout = new ButtonLayoutDescriptor
        {
            Mode = ButtonLayoutMode.Custom,
            Layers =
            [
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Indicator,
                    Name = "Battery arc",
                    IconScale = 0.78,
                    Color = "#34D399",
                    TrackColor = "#33FFFFFF",
                    Thickness = 0.09,
                    StartAngle = -90,
                    SweepAngle = 360
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text,
                    Name = "Percent",
                    TextSource = ButtonTextSource.Value,
                    TextSize = 20
                },
                new ButtonLayerDescriptor
                {
                    Kind = ButtonLayerKind.Text,
                    Name = "Status",
                    TextSource = ButtonTextSource.Detail,
                    TextSize = 9,
                    OffsetY = 16
                }
            ]
        }
    };

    public ButtonTargets SupportedTargets => ButtonTargets.TouchButton;

    public TimeSpan UpdateInterval => TimeSpan.FromSeconds(2);

    public AdjustmentValue? GetValue(CommandContext ctx)
    {
        HeadsetStatus status = monitor.Status;

        if (status.BatteryPercent is not int percent || percent is < 0 or > 100)
        {
            // No reading: show the status text without an arc (NaN = no indicator).
            return new AdjustmentValue(double.NaN, "—") { Detail = status.StatusText };
        }

        return new AdjustmentValue(percent / 100.0, $"{percent}%")
        {
            Detail = status.StatusText
        };
    }

    /// <summary>Pressing the button reads the headset immediately.</summary>
    public async Task Execute(CommandContext ctx)
    {
        try
        {
            await monitor.RefreshNowAsync().ConfigureAwait(false);
            ctx.Host.RequestButtonRefresh(PluginConstants.BatteryCommandName);
        }
        catch (Exception ex)
        {
            ctx.Host.Logger.Error("Reading the headset battery failed", ex);
        }
    }
}
