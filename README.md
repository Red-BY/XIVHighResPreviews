# XIV High Res Previews

Dalamud plugin that raises the render resolution of **in-game character preview windows** — Character, Try On, Inspect, Glamour / plates, banners, and similar UIs. Those views are drawn offscreen and usually look softer than the main game.

## Features

* Upscales live CharaView render targets (default **2×**)
* Auto-applies when a preview UI opens
* Scales matching viewports and skips BC plate chrome by default

## Install

This plugin is **not** on the official Dalamud plugin repository yet.

1. Build from source (see [Development](#development)), **or** install from a custom plugin repository if you publish one.
2. Enable **XIV High Res Previews** in `/xlplugins`.
3. Open `/xivhrp` to adjust settings.

## Usage

| Command | Action |
|---------|--------|
| `/xivhrp` | Open settings (enable + scale) |
| `/xivhrp apply` | Manually re-apply upscaling |
| `/xivhrp debug` | Advanced options + CharaView inspector |

Leave **Enable upscaling** on and set **Resolution scale** (e.g. `2.0×`). Open Character, Try On, Inspect, or Adventurer Plate — previews should look sharper.

## Known limitations

* Adventurer Plate backgrounds can still glitch in some cases.
* After changing scale, re-open the preview UI (or run `/xivhrp apply`) if buffers stay at the previous size.
* Higher scales increase GPU cost while a preview is open.
* Relies on ClientStructs field layouts; a game patch may require an update.

## Development

### Prerequisites

* XIVLauncher + Dalamud (game launched with Dalamud at least once)
* .NET SDK compatible with `Dalamud.NET.Sdk` (see CI: 10.0.x)
* Optional: `DALAMUD_HOME` if Dalamud is not in the default XIVLauncher path

### Build

```bash
dotnet build XIVHighResPreviews.slnx -c Release
```

Output: `XIVHighResPreviews/bin/x64/Release/XIVHighResPreviews/`

### Load as a dev plugin

1. `/xlsettings` → Experimental → add the folder containing `XIVHighResPreviews.dll` as a Dev Plugin Location
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable **XIV High Res Previews**

### How it works

Preview windows use `Client::UI::Misc::CharaView` and offscreen buffers owned by `RenderTargetManager` / `OffscreenRenderingManager`. Native live size is typically **576×960**. This plugin replaces those live render targets at the configured scale and, while a preview addon is open, scales matching `SetViewport` rects so the game does not draw into a corner of the larger texture.

Useful ClientStructs references:

* [CharaView.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Misc/CharaView.cs)
* [RenderTargetManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/RenderTargetManager.cs)
* [OffscreenRenderingManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/OffscreenRenderingManager.cs)

## License

AGPL-3.0 (inherited from the SamplePlugin template). See `LICENSE.md`.
