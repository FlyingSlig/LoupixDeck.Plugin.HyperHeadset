using System.Text.Json;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>Charging state reported by HyperHeadset.</summary>
internal enum ChargingState
{
    /// <summary>No charging information was reported.</summary>
    Unknown,

    /// <summary>The headset is running on battery.</summary>
    NotCharging,

    /// <summary>The battery is charging.</summary>
    Charging,

    /// <summary>The battery is full and still connected to power.</summary>
    FullyCharged,

    /// <summary>The headset reported a charging fault.</summary>
    Error
}

/// <summary>
/// One snapshot of the headset as reported by <c>hyper_headset_cli --json</c>.
/// </summary>
internal sealed record HeadsetStatus(
    int? BatteryPercent,
    ChargingState Charging,
    bool Connected,
    bool MicMuted,
    string? Error)
{
    /// <summary>The state before the first read completed.</summary>
    public static HeadsetStatus Unknown { get; } = new(null, ChargingState.Unknown, false, false, null);

    /// <summary>No usable reading — the executable is missing, failed, or timed out.</summary>
    public static HeadsetStatus Unavailable(string error) =>
        new(null, ChargingState.Unknown, false, false, error);

    /// <summary>True when the headset reported a usable battery percentage.</summary>
    public bool HasBattery => BatteryPercent is >= 0 and <= 100;

    /// <summary>
    /// Short text for the button's detail layer: the charging state while connected,
    /// otherwise a short reason.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (Connected)
            {
                return Charging switch
                {
                    ChargingState.Charging => "Charging",
                    ChargingState.FullyCharged => "Fully charged",
                    ChargingState.NotCharging => "Not charging",
                    ChargingState.Error => "Charging error",
                    _ => string.Empty
                };
            }

            if (Error is null)
                return "Reading…";
            if (Error.Contains("No device found", StringComparison.OrdinalIgnoreCase))
                return "No headset";
            if (Error.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return "Timed out";
            return "Unavailable";
        }
    }

    /// <summary>One-line description combining the percentage and the status, for overlays.</summary>
    public string SummaryText => HasBattery
        ? string.IsNullOrEmpty(StatusText)
            ? $"{BatteryPercent}%"
            : $"{BatteryPercent}% · {StatusText}"
        : StatusText;

    /// <summary>Parses the JSON document printed by <c>hyper_headset_cli --json</c>.</summary>
    /// <exception cref="JsonException">The text is not valid JSON.</exception>
    public static HeadsetStatus Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        int? battery = ReadBattery(root);
        bool connected = ReadBool(root, "connected") ?? battery is not null;

        return new HeadsetStatus(
            battery,
            ReadCharging(root),
            connected,
            ReadBool(root, "mic_muted") ?? false,
            null);
    }

    private static int? ReadBattery(JsonElement root)
    {
        if (!root.TryGetProperty("battery_level", out JsonElement element)
            || element.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (element.TryGetInt32(out int percent))
            return Math.Clamp(percent, 0, 100);
        if (element.TryGetDouble(out double value))
            return Math.Clamp((int)Math.Round(value), 0, 100);
        return null;
    }

    private static ChargingState ReadCharging(JsonElement root)
    {
        if (!root.TryGetProperty("charging_status", out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return ChargingState.Unknown;
        }

        return element.GetString() switch
        {
            "Charging" => ChargingState.Charging,
            "Fully charged" => ChargingState.FullyCharged,
            "Not charging" => ChargingState.NotCharging,
            "Charging error!" => ChargingState.Error,
            _ => ChargingState.Unknown
        };
    }

    private static bool? ReadBool(JsonElement root, string name)
        => root.TryGetProperty(name, out JsonElement element)
           && element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;
}
