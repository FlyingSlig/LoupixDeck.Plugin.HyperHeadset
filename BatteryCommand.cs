using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>
/// Touch-button command that shows the headset's battery level as a gauge and its charging
/// status as text. The arc is a declarative value layer the user can restyle; the text is drawn
/// by the plugin so it can carry an outline (button text layers have no outline property in the
/// SDK). The host polls <see cref="GetValue"/> and <see cref="RenderImage"/>; the underlying read
/// runs on the monitor's background loop, so both only return a cached snapshot.
/// </summary>
internal sealed class BatteryCommand(HeadsetMonitor monitor) : IValueDisplayCommand, IDisplayImageCommand
{
    private static readonly PluginColor OutlineColor = PluginColor.Black;
    private static readonly PluginColor PercentColor = PluginColor.White;
    private static readonly PluginColor StatusColor = new(210, 210, 218);

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
            // Transparent: the button shows the device background instead of a filled key.
            BackgroundColor = "#00000000",
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
            // No reading: show no arc, but keep the Value/Detail available for a text layer the
            // user may add in the button editor.
            return new AdjustmentValue(double.NaN, "—") { Detail = status.StatusText };
        }

        return new AdjustmentValue(percent / 100.0, $"{percent}%")
        {
            Detail = status.StatusText
        };
    }

    public bool RenderImage(CommandContext ctx, IRenderCanvas canvas)
    {
        HeadsetStatus status = monitor.Status;
        string percent = status.HasBattery ? $"{status.BatteryPercent}%" : "—";
        string detail = status.StatusText;

        int width = canvas.Width;
        int height = canvas.Height;
        float scale = Math.Min(width, height) / 90f;

        canvas.DrawText(
            percent,
            0,
            (int)(18 * scale),
            width,
            (int)(28 * scale),
            PercentColor,
            20 * scale,
            bold: true,
            centered: true,
            outlined: true,
            outlineColor: OutlineColor);

        if (detail.Length > 0)
        {
            canvas.DrawText(
                detail,
                0,
                (int)(50 * scale),
                width,
                (int)(16 * scale),
                StatusColorFor(status),
                9 * scale,
                centered: true,
                outlined: true,
                outlineColor: OutlineColor);
        }

        return true;
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

    private static PluginColor StatusColorFor(HeadsetStatus status) => status.Charging switch
    {
        ChargingState.Charging or ChargingState.FullyCharged => new PluginColor(52, 211, 153),
        ChargingState.Error => new PluginColor(248, 113, 113),
        _ => StatusColor
    };
}
