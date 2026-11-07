# LoupixDeck.Plugin.HyperHeadset

A [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) plugin that shows the battery level and
charging status of a HyperX headset on a touch button.

It reads the headset through [`hyper_headset_cli`](https://github.com/LennardKittner/HyperHeadset),
the CLI that ships with HyperHeadset, and renders:

- a **battery arc** showing the charge level,
- the **percentage** as text,
- the **charging status** (`Charging`, `Fully charged`, `Not charging`, `Charging error`)
  underneath.

When the headset is disconnected, the arc disappears, the caption reads `No headset`, and
LoupixDeck dims the button. Pressing the button reads the headset immediately and redraws it with
the fresh value.

The plugin does **not** enable a key background by default, so the device/theme background shows
through. If a key already carried a background from an earlier version, turn its **Background**
toggle off once.

> [!NOTE]
> **Outline:** the plugin cannot pre-enable the button editor's **Outline** option for its text
> layers. Plugin SDK 1.36's `ButtonLayerDescriptor` has no outline property, so the layers are
> created with the host default (no outline). You can turn **Outline** on once in the button
> editor; it is saved with the button afterwards.

> [!NOTE]
> **AI disclaimer:** this project was written with the help of an AI coding assistant. It has been
> built and tested against the author's headset and LoupixDeck installation, but it comes with no
> warranty. Review the code before relying on it.

> [!WARNING]
> **Tested on Linux only.** The plugin uses the cross-platform HyperHeadset CLI, so it may work on
> Windows (and macOS), but Windows support is untested — it might or might not work. Please report
> what you find if you try it.

## Requirements

- LoupixDeck with plugin SDK **1.36**
- [HyperHeadset](https://github.com/LennardKittner/HyperHeadset) installed, with
  `hyper_headset_cli` on `PATH` (or configure its full path in the plugin settings)
- Linux (tested); Windows and macOS are untested

The plugin supports whatever headsets HyperHeadset supports.

## Install

The plugin folder must be named `hyperheadset`, matching `PluginMetadata.Id` and `plugin.json`.

With the LoupixDeck plugin tool:

```bash
loupix install
```

Or build and copy it manually:

```bash
git clone https://github.com/FlyingSlig/LoupixDeck.Plugin.HyperHeadset.git
cd LoupixDeck.Plugin.HyperHeadset
dotnet build -c Release
mkdir -p ~/.config/LoupixDeck/plugins/hyperheadset
cp bin/Release/LoupixDeck.Plugin.HyperHeadset.dll \
   bin/Release/LoupixDeck.Plugin.HyperHeadset.deps.json \
   plugin.json \
   ~/.config/LoupixDeck/plugins/hyperheadset/
```

Restart LoupixDeck, then assign **HyperX Headset → Battery** to a touch button.

## Settings

The plugin adds a settings page to LoupixDeck:

| Setting | Kind | Default | Notes |
|---|---|---|---|
| `hyper_headset_cli path` | Text | `hyper_headset_cli` | Full path or a name found on `PATH`. `~/…` is expanded. |
| `Poll interval (s)` | Number | `30` | Clamped to 5–3600 seconds. |

There is also a **Read now** action for a one-off query.

## How it works

`hyper_headset_cli --json` prints a JSON object such as:

```json
{
  "charging_status": "Not charging",
  "battery_level": 58,
  "mic_muted": false,
  "connected": true
}
```

One read takes roughly 3 seconds because the CLI opens the HID device, so the plugin runs it on a
background loop and the button only reads a cached snapshot. Pressing the button triggers an
immediate read. `HYPERHEADSET_NO_AUTO_UDEV=1` is set for the child process so it never blocks on a
udev-rules prompt while LoupixDeck is running.

The plugin reports a missing `hyper_headset_cli` through `IPluginRequirements`, so LoupixDeck can
tell the user what to install.

## Build

```bash
dotnet build -c Release
```

The output is `bin/Release/LoupixDeck.Plugin.HyperHeadset.dll` (plus `.deps.json`). The SDK DLL is
not copied — the host provides it.

## Releasing

`plugin.json` is set up for `loupix pack` / the Plugin Store release workflow. Bump the version in
both `plugin.json` and `HyperHeadsetPlugin.Metadata`, then publish a GitHub release tagged
`v<version>`; `.github/workflows/release.yml` builds and attaches the store files.

## License

MIT — see [LICENSE](LICENSE).
