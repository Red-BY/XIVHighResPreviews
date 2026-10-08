using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace XIVHighResPreviews.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("XIV High Res Previews — Settings###Config")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = configuration.EnablePreviewUpscale;
        if (ImGui.Checkbox("Enable preview upscale (not applied yet)", ref enabled))
        {
            configuration.EnablePreviewUpscale = enabled;
            configuration.Save();
        }

        var scale = configuration.PreviewResolutionScale;
        if (ImGui.SliderFloat("Preview resolution scale", ref scale, 1.0f, 4.0f, "%.2fx"))
        {
            configuration.PreviewResolutionScale = Math.Clamp(scale, 1.0f, 4.0f);
            configuration.Save();
        }

        ImGui.TextDisabled("Changes will take effect once render-target resizing is implemented.");
    }
}
