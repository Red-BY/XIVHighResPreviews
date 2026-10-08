# XIV High Res Previews

Dalamud plugin that aims to raise the render resolution of **in-game character preview windows** (Character, Try On, Inspect, Glamour / plates, banners, etc.). Those views are drawn by a separate offscreen path and usually do **not** match the main game resolution.

## Status

Scaffold + live **CharaView render-target inspector**. Upscaling is not applied yet — config stores a scale factor for the next step.

| Command | Action |
|---------|--------|
| `/xivhrp` | Open the inspector |

## Development

### Prerequisites

* XIVLauncher + Dalamud (game launched with Dalamud at least once)
* .NET SDK compatible with `Dalamud.NET.Sdk` (see CI: 10.0.x)
* Optional: `DALAMUD_HOME` if Dalamud is not in the default XIVLauncher path

### Build

```bash
dotnet build XIVHighResPreviews.slnx -c Debug
```

Output: `XIVHighResPreviews/bin/x64/Debug/XIVHighResPreviews/`

### Load in-game

1. `/xlsettings` → Experimental → add the folder containing `XIVHighResPreviews.dll` as a Dev Plugin Location
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable **XIV High Res Previews**
3. `/xivhrp` while a preview UI is open

## Research notes — finding preview render targets

FFXIVClientStructs already maps the relevant graphics objects. This is the path the inspector uses.

### Key types

1. **`Client::UI::Misc::CharaView`**  
   Logical preview controller used by Character, Inspect, Try On, etc.  
   Important fields: `ClientObjectIndex` (0–7), `State`, camera, agent callbacks.  
   Docs comment which UI uses which index (0 = Character, 1 = Inspect/CharaCard, 2 = Try On, …).

2. **`Client::Graphics::Render::OffscreenRenderingManager`**  
   Renderer responsible for CharaViews. Holds up to **8 cameras** and **8 background textures**.  
   Singleton: `OffscreenRenderingManager.Instance()`.

3. **`Client::Graphics::Render::RenderTargetManager`**  
   Owns the actual CharaView framebuffer set:
   - `CharaViewTextures[8]` — final preview images
   - `CharaViewGBuffers` / depth / view-position equivalents
   - `CharaViewSemitransparentGBuffers`
   - `GetCharaViewTexture(clientObjectIndex)` — preferred accessor for the result texture  
   Also exposes main `Resolution_Width` / `Resolution_Height` and `GraphicsRezoScale` (main scene; not necessarily CharaView size).

4. **`Client::Graphics::Kernel::Texture`**  
   Read size from `ActualWidth` / `ActualHeight` (and `AllocatedWidth` / `AllocatedHeight` when dynamic resolution pads the allocation).

5. **`GraphicsConfig`**  
   Has main-scene resolution scale (`GraphicsRezoScale`) and portrait/GPose flags. **No dedicated CharaView resolution field** is mapped today — preview size is likely chosen inside the render-target (re)allocation path, not a simple config option.

### Practical inspection loop

1. Open `/xivhrp`.
2. Open an in-game preview (e.g. Character window → slot 0, Try On → slot 2).
3. Note `ActualWidth×ActualHeight` on the matching slot vs main RT resolution.
4. That delta is what we want to change.

### Likely next steps to *change* resolution

| Approach | Idea | Risk |
|----------|------|------|
| Hook texture creation used for CharaView RTs | Intercept width/height when `CreateTexture2D` (or the RTM recreate path) runs for CharaView buffers | Need a reliable way to identify CharaView allocations vs main scene |
| Patch / call RT recreate after patching size inputs | Find the function that (re)builds `_charaViewTextures` / G-buffers and feed larger dimensions | Signature maintenance every patch |
| Replace textures after creation | Allocate larger `Texture`s and swap pointers in `RenderTargetManager` | Must also fix any viewport/scissor/UI draw that assumes original size |
| Agent / Atk image node scale only | Upscale how the UI displays the texture | Does **not** increase render quality; only display size |

Haselnussbomber’s ClientStructs work on `RenderTargetManager` (Dawntrail) noted interest in CharaView rendering but stopped at field mapping — so the recreate/size source is still an open reverse-engineering target.

Useful starting signatures (from ClientStructs, verify each patch):

* `RenderTargetManager.Instance` static address
* `GetCharaViewTexture` member function
* `OffscreenRenderingManager.Instance` static address
* `Texture.CreateTexture2D` / `Device.CreateTexture2D`

### References

* [OffscreenRenderingManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/OffscreenRenderingManager.cs)
* [RenderTargetManager.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Render/RenderTargetManager.cs)
* [CharaView.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Misc/CharaView.cs)
* [Texture.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Graphics/Kernel/Texture.cs)
* [ClientStructs PR #1128](https://github.com/aers/FFXIVClientStructs/pull/1128) (RenderTargetManager / GraphicsConfig update)

## License

AGPL-3.0 (inherited from the SamplePlugin template). See `LICENSE.md`.
