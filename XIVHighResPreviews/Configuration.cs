using Dalamud.Configuration;
using System;

namespace XIVHighResPreviews;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 8;

    /// <summary>
    /// Multiplier applied to live CharaView render targets and matching viewports.
    /// 1.0 = game default; values above 1 upscale.
    /// </summary>
    public float PreviewResolutionScale { get; set; } = 2.0f;

    /// <summary>Master toggle for applying <see cref="PreviewResolutionScale"/>.</summary>
    public bool EnablePreviewUpscale { get; set; } = true;

    /// <summary>
    /// Automatically replace live CharaView textures that still have native preview sizes.
    /// </summary>
    public bool AutoUpscaleLiveCharaView { get; set; } = true;

    /// <summary>
    /// Scale SetViewport rects that still use the pre-upscale preview size.
    /// Hooks are only enabled while a preview UI is open.
    /// </summary>
    public bool ScalePreviewViewports { get; set; } = true;

    /// <summary>
    /// Skip BC-compressed textures (plate chrome / backgrounds) when replacing live RTs.
    /// </summary>
    public bool SkipScalingColorUnorm { get; set; } = true;

    /// <summary>Log viewport / clear rect scale events at Debug level.</summary>
    public bool LogViewportScales { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
