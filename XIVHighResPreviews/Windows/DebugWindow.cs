using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace XIVHighResPreviews.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public DebugWindow(Plugin plugin)
        : base("High Resolution Previews — Debug##Debug")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawTools();

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        DrawInspector();
    }

    private void DrawTools()
    {
        var cfg = plugin.Configuration;

        ImGui.TextUnformatted("Debug options");

        var autoLive = cfg.AutoUpscaleLiveCharaView;
        if (ImGui.Checkbox("Auto-upscale live CharaView", ref autoLive))
        {
            cfg.AutoUpscaleLiveCharaView = autoLive;
            cfg.Save();
        }

        ImGui.TextDisabled("Replaces native-sized preview RTs shortly after they appear.");

        var skipBc = cfg.SkipScalingColorUnorm;
        if (ImGui.Checkbox("Skip BC-compressed textures (plate chrome)", ref skipBc))
        {
            cfg.SkipScalingColorUnorm = skipBc;
            cfg.Save();
        }

        var scaleVp = cfg.ScalePreviewViewports;
        if (ImGui.Checkbox("Scale matching viewports", ref scaleVp))
        {
            cfg.ScalePreviewViewports = scaleVp;
            cfg.Save();
        }

        var logVp = cfg.LogViewportScales;
        if (ImGui.Checkbox("Log viewport scales", ref logVp))
        {
            cfg.LogViewportScales = logVp;
            cfg.Save();
        }

        ImGuiHelpers.ScaledDummy(4f);

        if (ImGui.Button("Upscale live CharaView now"))
        {
            plugin.LiveUpscaler.MarkNeedsRescan();
            plugin.RequestLiveApply();
        }

        ImGui.TextWrapped(plugin.LiveUpscaler.LastStatus);

        ImGui.Text(
            $"vp hook={(plugin.ViewportHook.IsEnabled ? "live" : "idle")}  |  scales={plugin.ViewportHook.ViewportScaleCount}  |  previewOpen={plugin.PreviewAddons.OpenCount}  |  satisfied={plugin.LiveUpscaler.IsSatisfied}");
    }

    private void DrawInspector()
    {
        ImGui.TextUnformatted("CharaView inspector");
        ImGui.TextWrapped("Open Character, Try On, Inspect, Adventurer Plate, etc., then watch the matching slot.");

        ImGuiHelpers.ScaledDummy(4f);

        var snap = plugin.Inspector.Capture();

        ImGui.Text($"RenderTargetManager: {(snap.RenderTargetManagerAvailable ? "ok" : "null")}");
        ImGui.SameLine();
        ImGui.Text($"| OffscreenRenderingManager: {(snap.OffscreenManagerAvailable ? "ok" : "null")}");

        if (snap.RenderTargetManagerAvailable)
        {
            ImGui.Text(
                $"Main RT resolution: {snap.MainResolutionWidth}×{snap.MainResolutionHeight}  |  GraphicsRezoScale: {snap.GraphicsRezoScale:F3} / {snap.GraphicsRezoScaleY:F3}");
        }

        DrawPreviewSizeSummary(snap);
        DrawNativeSizeHint(snap);

        ImGuiHelpers.ScaledDummy(6f);

        if (!ImGui.BeginTable("CharaViewSlots", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Slot");
        ImGui.TableSetupColumn("Live size");
        ImGui.TableSetupColumn("Format");
        ImGui.TableSetupColumn("Background");
        ImGui.TableSetupColumn("Flags");
        ImGui.TableHeadersRow();

        foreach (var slot in snap.Slots)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(slot.Label);

            ImGui.TableNextColumn();
            if (slot.HasTexture && slot.Texture is { } tex)
                ImGui.Text($"{tex.ActualWidth}×{tex.ActualHeight}");
            else
                ImGui.TextDisabled("—");

            ImGui.TableNextColumn();
            if (slot.HasTexture && slot.Texture is { } texF)
                ImGui.TextUnformatted(ShortFormat(texF.Format));
            else
                ImGui.TextDisabled("—");

            ImGui.TableNextColumn();
            if (slot.HasBackground && slot.Background is { } bg)
                ImGui.Text($"{bg.ActualWidth}×{bg.ActualHeight}");
            else
                ImGui.TextDisabled("—");

            ImGui.TableNextColumn();
            if (slot.HasTexture && slot.Texture is { } texFlags)
                ImGui.Text($"{(uint)texFlags.Flags:X}");
            else if (slot.HasBackground && slot.Background is { } bgFlags)
                ImGui.Text($"{(uint)bgFlags.Flags:X}");
            else
                ImGui.TextDisabled("—");
        }

        ImGui.EndTable();
    }

    private void DrawNativeSizeHint(CharaViewInspector.Snapshot snap)
    {
        var native = 0;
        foreach (var slot in snap.Slots)
        {
            if (!slot.HasTexture || slot.Texture is not { } tex)
                continue;

            if (PreviewSizes.TryIdentify((int)tex.ActualWidth, (int)tex.ActualHeight, out _)
                || (tex.ActualWidth == 576 && tex.ActualHeight == 960))
            {
                native++;
            }
        }

        if (native == 0)
            return;

        ImGui.TextColored(
            new Vector4(1f, 0.55f, 0.2f, 1f),
            plugin.Configuration.AutoUpscaleLiveCharaView && plugin.Configuration.EnablePreviewUpscale
                ? $"{native} slot(s) still at native size — auto-upscale should replace them shortly."
                : $"{native} slot(s) still at native size. Enable upscaling / auto-apply, or use “Upscale live CharaView now”.");
    }

    private static string ShortFormat(FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.TextureFormat format)
    {
        var name = format.ToString();
        return name.Length <= 18 ? name : name[..16] + "…";
    }

    private static void DrawPreviewSizeSummary(CharaViewInspector.Snapshot snap)
    {
        uint? sharedW = null;
        uint? sharedH = null;
        var active = 0;
        var allSame = true;

        foreach (var slot in snap.Slots)
        {
            if (!slot.HasTexture || slot.Texture is not { } tex)
                continue;

            active++;
            if (sharedW == null)
            {
                sharedW = tex.ActualWidth;
                sharedH = tex.ActualHeight;
            }
            else if (sharedW != tex.ActualWidth || sharedH != tex.ActualHeight)
            {
                allSame = false;
            }
        }

        if (active == 0)
        {
            ImGui.TextDisabled("No active CharaView textures — open a preview window.");
            return;
        }

        if (allSame && sharedW is { } w && sharedH is { } h)
        {
            var vsMain = snap.MainResolutionWidth > 0
                ? $"  |  vs main width: {(float)w / snap.MainResolutionWidth:P0}"
                : string.Empty;
            ImGui.Text($"Active preview size: {w}×{h} ({AspectLabel(w, h)}) across {active} slot(s){vsMain}");
        }
        else
        {
            ImGui.Text($"Active preview slots: {active} (sizes differ — see table)");
        }
    }

    private static string AspectLabel(uint w, uint h)
    {
        static uint Gcd(uint a, uint b)
        {
            while (b != 0)
            {
                var t = b;
                b = a % b;
                a = t;
            }

            return a;
        }

        var g = Gcd(w, h);
        return g == 0 ? "?" : $"{w / g}:{h / g}";
    }
}
