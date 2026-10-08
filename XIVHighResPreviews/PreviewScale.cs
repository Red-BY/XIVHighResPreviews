using System;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace XIVHighResPreviews;

/// <summary>
/// Shared scale helpers for live CharaView replacements and viewport rects.
/// </summary>
public static class PreviewScale
{
    public const float MinScale = 1.0f;
    public const float MaxScale = 4.0f;

    public static bool IsActive(Configuration cfg)
        => cfg.EnablePreviewUpscale && cfg.PreviewResolutionScale > 1.001f;

    public static int ScaleDimension(int value, float scale)
    {
        var scaled = (int)MathF.Round(value * scale);
        if (scaled < 2)
            scaled = 2;
        if ((scaled & 1) != 0)
            scaled--;
        return Math.Max(scaled, value);
    }

    public static bool IsCompressedFormat(TextureFormat format)
        => format is TextureFormat.BC1_UNORM
            or TextureFormat.BC2_UNORM
            or TextureFormat.BC3_UNORM
            or TextureFormat.BC4_UNORM
            or TextureFormat.BC5_UNORM
            or TextureFormat.BC6H_SF16
            or TextureFormat.BC7_UNORM;

    public static bool TryScalePreviewRect(ref IntRectangle rect, Configuration cfg, out string label)
    {
        label = string.Empty;
        if (!IsActive(cfg) || !cfg.ScalePreviewViewports)
            return false;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return false;

        if (!PreviewSizes.ShouldInterceptPrimary(width, height, out label))
            return false;

        var scale = cfg.PreviewResolutionScale;
        rect.Right = rect.Left + ScaleDimension(width, scale);
        rect.Bottom = rect.Top + ScaleDimension(height, scale);
        return true;
    }
}
