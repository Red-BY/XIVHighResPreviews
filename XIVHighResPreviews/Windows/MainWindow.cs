using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace XIVHighResPreviews.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("XIV High Res Previews##Main")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped(
            "Live inspector for CharaView offscreen render targets. Open Character, Try On, Inspect, etc., then watch the matching slot populate.");

        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        ImGui.TextDisabled("Command: /xivhrp");

        ImGuiHelpers.ScaledDummy(8f);

        var snap = plugin.Inspector.Capture();

        ImGui.Text($"RenderTargetManager: {(snap.RenderTargetManagerAvailable ? "ok" : "null")}");
        ImGui.SameLine();
        ImGui.Text($"| OffscreenRenderingManager: {(snap.OffscreenManagerAvailable ? "ok" : "null")}");

        if (snap.RenderTargetManagerAvailable)
        {
            ImGui.Text(
                $"Main RT resolution: {snap.MainResolutionWidth}×{snap.MainResolutionHeight}  |  GraphicsRezoScale: {snap.GraphicsRezoScale:F3} / {snap.GraphicsRezoScaleY:F3}");
        }

        ImGuiHelpers.ScaledDummy(6f);

        if (!ImGui.BeginTable("CharaViewSlots", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Slot");
        ImGui.TableSetupColumn("Preview texture");
        ImGui.TableSetupColumn("Allocated");
        ImGui.TableSetupColumn("Format");
        ImGui.TableSetupColumn("Background");
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
            if (slot.HasTexture && slot.Texture is { } texA)
                ImGui.Text($"{texA.AllocatedWidth}×{texA.AllocatedHeight}");
            else
                ImGui.TextDisabled("—");

            ImGui.TableNextColumn();
            if (slot.HasTexture && slot.Texture is { } texF)
                ImGui.TextUnformatted(texF.Format.ToString());
            else
                ImGui.TextDisabled("—");

            ImGui.TableNextColumn();
            if (slot.HasBackground && slot.Background is { } bg)
                ImGui.Text($"{bg.ActualWidth}×{bg.ActualHeight}");
            else
                ImGui.TextDisabled("—");
        }

        ImGui.EndTable();

        ImGuiHelpers.ScaledDummy(10f);
        ImGui.Separator();
        ImGui.TextWrapped(
            "Next step: find where these textures are created/resized (likely alongside RenderTargetManager CharaView G-buffers) and intercept that path to allocate larger targets. Scale setting is stored but not applied yet.");
    }
}
