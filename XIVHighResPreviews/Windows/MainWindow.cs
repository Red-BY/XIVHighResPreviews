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
        : base("High Resolution Previews##Main")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 0),
            MaximumSize = new Vector2(480, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        var enabled = cfg.EnablePreviewUpscale;
        if (ImGui.Checkbox("Enable upscaling", ref enabled))
        {
            cfg.EnablePreviewUpscale = enabled;
            cfg.Save();
            plugin.RequestResetAndReapply();
        }

        ImGui.BeginDisabled(!cfg.EnablePreviewUpscale);

        var scale = cfg.PreviewResolutionScale;
        if (ImGui.SliderFloat("Resolution scale", ref scale, PreviewScale.MinScale, PreviewScale.MaxScale, "%.2fx"))
        {
            cfg.PreviewResolutionScale = Math.Clamp(scale, PreviewScale.MinScale, PreviewScale.MaxScale);
            cfg.Save();
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
            plugin.RequestResetAndReapply();

        ImGui.TextDisabled("1.00× = game default. Higher looks sharper but costs more GPU.");

        ImGui.EndDisabled();

        ImGuiHelpers.ScaledDummy(6f);

        if (cfg.EnablePreviewUpscale)
            ImGui.Text($"Status: on @ {cfg.PreviewResolutionScale:F2}× — {plugin.LiveUpscaler.LastStatus}");
        else
            ImGui.TextDisabled($"Status: off — {plugin.LiveUpscaler.LastStatus}");
    }
}
