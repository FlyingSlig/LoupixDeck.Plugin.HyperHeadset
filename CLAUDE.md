# LoupixDeck.Plugin.HyperHeadset

LoupixDeck plugin that shows a HyperX headset's battery level and charging status on a touch
button. Data comes from `hyper_headset_cli --json` (HyperHeadset).

## Build and install

```bash
dotnet build -c Release
loupix install          # or copy bin/Release + plugin.json into ~/.config/LoupixDeck/plugins/hyperheadset/
```

Restart LoupixDeck afterwards. The plugin id, `plugin.json#id` and the plugin folder name are all
`hyperheadset`.

## Layout

- `HyperHeadsetPlugin.cs` — `LoupixPlugin` entry point, settings page (`IPluginSettingsPage`) and
  requirements (`IPluginRequirements`).
- `BatteryCommand.cs` — the only command; implements `IValueDisplayCommand` and declares its
  button layout (arc + value + detail).
- `HeadsetMonitor.cs` — background poll loop, cached status, settings restart, `IDisposable`.
- `HyperHeadsetClient.cs` — spawns `hyper_headset_cli --json`, enforces a timeout, extracts JSON.
- `HeadsetStatus.cs` — snapshot record + JSON parsing + display strings.
- `ExecutableLocator.cs` — PATH lookup for the requirement check.

## Rules and gotchas

1. **One** concrete `LoupixPlugin` subclass per assembly.
2. `CommandName` (`HyperHeadset.Battery`) is a stable public API — never rename it.
3. The SDK DLL is provided by the host; never ship `LoupixDeck.PluginSdk.dll`.
4. Only tested on Linux so far. The CLI is cross-platform, but Windows/macOS behaviour is unverified.
5. A read takes ~3 s because the CLI opens the HID device. Never run it from `GetValue`,
   `GetText` or any other polled/render callback — only from the monitor's background loop.
6. `HYPERHEADSET_NO_AUTO_UDEV=1` is set for the child process. With it, the CLI prints a banner
   to **stdout** before the JSON, so the client extracts the `{…}` object instead of parsing the
   whole stream.
7. Charging status is event-driven and may be absent from a read; `HeadsetMonitor` keeps the last
   known value while connected.
8. The SDK's declarative button text layers have **no outline property** (`TextLayer.Outlined`
   exists in the host, but `ButtonLayerDescriptor` does not expose it), so the plugin cannot
   pre-enable the editor's Outline option. Do not work around this by drawing the text in
   `RenderImage`; the user wants the editable text layers.
9. Keep `Metadata.Version` and `plugin.json#version` in sync.
