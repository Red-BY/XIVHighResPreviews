# High Resolution Previews

A Dalamud plugin that lets you customize the render resolution of **in-game character preview windows** — Character, Try On, Inspect, Glamour plates, banners, and similar UIs. Those views are drawn at a predefined resolution and usually look softer than the main game.

## Features

* Upscales live CharaView render targets (default **2×**)
* Applies automatically when a preview UI opens

## Install

This plugin is **not** on the official Dalamud plugin repository yet.

1. Build from source (see [Development](#development)), **or** install from a custom plugin repository if you publish one.
2. Enable **High Resolution Previews** in `/xlplugins`.
3. Open `/hrpreviews` to adjust settings.

## Usage

| Command | Action |
|---------|--------|
| `/hrpreviews` | Open settings (enable + scale) |
| `/hrpreviews debug` | Advanced options + CharaView inspector |

Leave **Enable upscaling** on and set **Resolution scale** (e.g. `2.0×`). When you open Character, Try On, Inspect, or Adventurer Plate, previews should look sharper.

## Known limitations

* After changing scale, reopen the preview UI if buffers stay at the previous size.
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
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable **High Resolution Previews**

### How it works

Preview windows use `Client::UI::Misc::CharaView` and offscreen buffers owned by `RenderTargetManager` / `OffscreenRenderingManager`. The native live size is typically **576×960**. This plugin replaces those live render targets at the configured scale and, while a preview addon is open, scales matching `SetViewport` rects so the game does not draw into a corner of the larger texture.

Useful ClientStructs references:

* [CharaView.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Misc/CharaView.cs)
* [RenderTargetManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/RenderTargetManager.cs)
* [OffscreenRenderingManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/OffscreenRenderingManager.cs)

## License

AGPL-3.0 (inherited from the SamplePlugin template). See `LICENSE.md`.
