using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>
/// LoupixDeck plugin that reports a HyperX headset's battery level and charging status.
/// The data comes from the <c>hyper_headset_cli</c> tool shipped with HyperHeadset.
/// </summary>
public sealed class HyperHeadsetPlugin : LoupixPlugin, IPluginSettingsPage, IPluginRequirements
{
    private IPluginHost? _host;
    private HeadsetMonitor? _monitor;

    public override PluginMetadata Metadata { get; } = new()
    {
        Id = "hyperheadset",
        Name = "HyperX Headset",
        Version = new Version(1, 0, 0),
        SdkVersion = SdkInfo.Version,
        Author = "FlyingSlig",
        Description = "Battery level and charging status for HyperX headsets via hyper_headset_cli."
    };

    public override void Initialize(IPluginHost host)
    {
        _host = host;

        (string executable, long seconds) = ReadSettings(host);
        _monitor = new HeadsetMonitor(host.Logger, executable, TimeSpan.FromSeconds(seconds));
        _monitor.StatusChanged += OnStatusChanged;
        _monitor.Start();

        host.Logger.Info($"Monitoring the HyperX headset every {seconds}s using '{executable}'.");
    }

    public override void Shutdown()
    {
        if (_monitor is null)
            return;

        _monitor.StatusChanged -= OnStatusChanged;
        _monitor.Dispose();
        _monitor = null;
    }

    public override IEnumerable<IPluginCommand> GetCommands()
    {
        if (_monitor is null)
            yield break;

        yield return new BatteryCommand(_monitor);
    }

    public override IReadOnlyList<CommandGroupDescriptor> GetCommandGroups() =>
    [
        new CommandGroupDescriptor
        {
            Group = PluginConstants.Group,
            Description = "Battery level and charging status of a HyperX headset.",
            Icon = PluginConstants.HeadsetGlyph,
            Section = CommandGroupSection.Plugins
        }
    ];

    // ── Settings page ───────────────────────────────────────────────────────

    public IReadOnlyList<PluginSettingDescriptor> SettingsSchema { get; } =
    [
        new PluginSettingDescriptor
        {
            Key = "cliPath",
            Label = "hyper_headset_cli path",
            Kind = PluginSettingKind.Text,
            DefaultValue = PluginConstants.DefaultExecutable,
            Description = "Executable used to query the headset. Leave as-is when it is on PATH."
        },
        new PluginSettingDescriptor
        {
            Key = "pollSeconds",
            Label = "Poll interval (s)",
            Kind = PluginSettingKind.Number,
            DefaultValue = PluginConstants.DefaultPollSeconds,
            Description = "How often the headset is queried. Each read takes a few seconds (5–3600)."
        }
    ];

    public IReadOnlyList<PluginSettingAction> SettingsActions =>
    [
        new PluginSettingAction
        {
            Label = "Read now",
            Invoke = async () =>
            {
                if (_monitor is null)
                    return "Plugin is not initialized.";

                HeadsetStatus status = await _monitor.RefreshNowAsync().ConfigureAwait(false);
                return status.SummaryText;
            }
        }
    ];

    public void OnSettingsSaved()
    {
        if (_host is null || _monitor is null)
            return;

        (string executable, long seconds) = ReadSettings(_host);
        _monitor.Restart(executable, TimeSpan.FromSeconds(seconds));
        _host.Logger.Info($"Headset polling now every {seconds}s using '{executable}'.");
    }

    // ── Requirements ────────────────────────────────────────────────────────

    public IReadOnlyList<PluginRequirement> GetRequirements()
    {
        string executable = _host is not null
            ? ReadSettings(_host).Executable
            : PluginConstants.DefaultExecutable;
        bool found = ExecutableLocator.Exists(executable);

        return
        [
            new PluginRequirement
            {
                Id = "hyper_headset_cli",
                Name = "hyper_headset_cli (HyperHeadset)",
                IsMet = found,
                Message = found
                    ? null
                    : $"'{executable}' was not found. The plugin cannot read the headset.",
                InstallHint = found
                    ? null
                    : "Install HyperHeadset from https://github.com/LennardKittner/HyperHeadset"
            }
        ];
    }

    private static (string Executable, long Seconds) ReadSettings(IPluginHost host)
    {
        string executable = host.Settings.Get<string>("cliPath") is { Length: > 0 } value
            ? value.Trim()
            : PluginConstants.DefaultExecutable;
        long seconds = Math.Clamp(
            host.Settings.Get<long>("pollSeconds", PluginConstants.DefaultPollSeconds),
            5,
            3600);

        return (executable, seconds);
    }

    private void OnStatusChanged(HeadsetStatus status)
    {
        if (_host is null)
            return;

        try
        {
            _host.SetAvailable(status.Connected);
        }
        catch (MissingMethodException)
        {
            // Host older than SDK 1.36: it has no availability dimming. Not fatal.
        }
        catch (Exception ex)
        {
            _host.Logger.Warn($"Could not update the plugin availability: {ex.Message}");
        }

        try
        {
            _host.RequestButtonRefresh(PluginConstants.BatteryCommandName);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn($"Could not refresh the battery button: {ex.Message}");
        }
    }
}
