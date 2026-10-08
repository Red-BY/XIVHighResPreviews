using Dalamud.Configuration;
using System;

namespace XIVHighResPreviews;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>
    /// Multiplier applied to CharaView render targets once resizing is implemented.
    /// 1.0 = game default; 2.0 = 2x linear resolution, etc.
    /// </summary>
    public float PreviewResolutionScale { get; set; } = 2.0f;

    public bool EnablePreviewUpscale { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
