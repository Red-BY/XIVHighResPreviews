# XIV High Res Previews

Dalamud plugin that aims to raise the render resolution of **in-game character preview windows** (Character, Try On, Inspect, Glamour / plates, banners, etc.). Those views are drawn by a separate offscreen path and usually do **not** match the main game resolution.

## Status

Live **CharaView inspector** plus **live RT replacement** of native-sized CharaView textures (auto on preview UI open). Viewport hooks are enabled only while a preview addon is open.

Create-time `CreateTexture2D` interception was removed: plate create scaling changed buffer sizes without sharpening the character model; replacing the live CharaView slots is what actually upscales the render.

| Command | Action |
|---------|--------|
| `/xivhrp` | Open settings (enable + scale) |
| `/xivhrp debug` | Open the CharaView inspector / debug options |
| `/xivhrp apply` | Manually upscale live CharaView textures |

### Trying it in-game

1. Rebuild and reload the plugin.
2. Settings: leave **Enable preview upscale** and **Auto-upscale live CharaView** on, scale e.g. `2.0`.
3. Open **Character**, **Try On**, **Inspect**, or **Adventurer Plate**.
4. In `/xivhrp`, live sizes should move off native (e.g. `576×960` → `1152×1920`).

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

### Empirical findings

| Observation | Size | Notes |
|-------------|------|-------|
| Live `CharaView` texture `ActualWidth×Height` | **576×960** (exact 3:5) | Independent of display / UI scale |
| `CreateTexture2D` for Character / preview windows | **192×320** (exact 3:5, **⅓ of 576×960**) | Primary create-time size for Character UI |
| `CreateTexture2D` on login | **288×480** (exact 3:5, **½ of 576×960**) | Often BC compressed |
| `CreateTexture2D` opening Adventurer Plate | **512×840** (≈3:5) | `2× BGRA` + `1× BC7`, flags often `0x804` |
| `CreateTexture2D` at 576×960 | **not observed** | Live size is 3× the Character create size |

Implications:

* Preview size is **not** derived from the main swapchain.
* Character create **`192×320 × 3 = 576×960`** — live Actual* may be a different/composited buffer, or metadata at display scale.
* Plate: upscale BGRA, skip BC7. Same Immutable-upload rules if flags match.

Hex immediates: `0xC0`/`0x140` (192×320), `0x120`/`0x1E0` (288×480), `0x200`/`0x348` (512×840), `0x240`/`0x3C0` (576×960).

### Implemented: live CharaView replace

Replaces native-sized `RenderTargetManager` CharaView textures / G-buffers / aux targets on preview addon open. Skips BC compressed when configured. Viewport scaling only while a preview UI is open. **Min scale 1.0×**. Auto-apply on by default.

### Remaining risks / next steps

| Issue | Follow-up |
|-------|-----------|
| Plate BG corruption | Mismatch between preview RT and background size |
| Scale change after apply | Re-open UI or `/xivhrp apply` if buffers stay at previous scale |

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
