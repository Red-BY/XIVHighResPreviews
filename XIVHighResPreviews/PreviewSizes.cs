using System;
using System.Collections.Generic;

namespace XIVHighResPreviews;

/// <summary>
/// Known CharaView / portrait sizes (live Actual* and historically observed create sizes).
/// </summary>
public static class PreviewSizes
{
    /// <summary>Live CharaView texture ActualWidth×Height in the inspector.</summary>
    public static readonly SizeSpec CharaViewReported = new(576, 960, "CharaView reported (Actual*)");

    /// <summary>
    /// Character / Try On / Inspect preview create size — exactly ⅓ of <see cref="CharaViewReported"/>.
    /// </summary>
    public static readonly SizeSpec CharacterPreview = new(192, 320, "Character / preview window");

    /// <summary>Exactly half of <see cref="CharaViewReported"/> — seen once on login (often BC).</summary>
    public static readonly SizeSpec LoginHalf = new(288, 480, "Login (half of 576×960)");

    /// <summary>Adventurer Plate / plate-style preview allocations.</summary>
    public static readonly SizeSpec AdventurerPlate = new(512, 840, "Adventurer Plate");

    /// <summary>Half of Adventurer Plate — possible aux / downsample target.</summary>
    public static readonly SizeSpec AdventurerPlateHalf = new(256, 420, "Adventurer Plate half");

    public static readonly IReadOnlyList<SizeSpec> PrimarySizes =
    [
        CharacterPreview,
        LoginHalf,
        AdventurerPlate,
        CharaViewReported,
    ];

    public static readonly IReadOnlyList<SizeSpec> RelatedHalfSizes =
    [
        AdventurerPlateHalf,
        new(96, 160, "Character preview half"),
        new(144, 240, "Login quarter"),
    ];

    public readonly record struct SizeSpec(int Width, int Height, string Label)
    {
        public bool Matches(int width, int height)
            => (width == Width && height == Height)
               || (width == Height && height == Width);
    }

    public static bool TryIdentify(int width, int height, out SizeSpec spec)
    {
        foreach (var known in PrimarySizes)
        {
            if (known.Matches(width, height))
            {
                spec = known;
                return true;
            }
        }

        foreach (var known in RelatedHalfSizes)
        {
            if (known.Matches(width, height))
            {
                spec = known;
                return true;
            }
        }

        spec = default;
        return false;
    }

    /// <summary>
    /// Viewport/clear hooks should only match primary native sizes so a game that already
    /// uses the scaled texture dimensions is not scaled a second time.
    /// </summary>
    public static bool ShouldInterceptPrimary(int width, int height, out string label)
    {
        foreach (var known in PrimarySizes)
        {
            if (known.Matches(width, height))
            {
                label = known.Label;
                return true;
            }
        }

        label = string.Empty;
        return false;
    }

    /// <summary>
    /// Map a live size back to a known native preview size (exact match or previously scaled).
    /// <paramref name="preferredScale"/> is tried first (last applied / configured scale).
    /// </summary>
    public static bool TryResolveNativeSize(int width, int height, out int nativeW, out int nativeH, float? preferredScale = null)
    {
        if (TryIdentify(width, height, out var exact))
        {
            nativeW = exact.Width;
            nativeH = exact.Height;
            if (width == exact.Height && height == exact.Width)
            {
                nativeW = exact.Height;
                nativeH = exact.Width;
            }

            return true;
        }

        if (preferredScale is > 1.001f
            && TryUnscaleWithScale(width, height, preferredScale.Value, out nativeW, out nativeH))
        {
            return true;
        }

        // Brute-force common scale steps in case preferred scale was lost (e.g. plugin reload).
        for (var s = PreviewScale.MaxScale; s >= 1.05f; s -= 0.05f)
        {
            if (preferredScale is float pref && MathF.Abs(s - pref) < 0.001f)
                continue;

            if (TryUnscaleWithScale(width, height, s, out nativeW, out nativeH))
                return true;
        }

        foreach (var known in PrimarySizes)
        {
            if (TryMatchScaled(known.Width, known.Height, width, height, out nativeW, out nativeH))
                return true;
        }

        foreach (var known in RelatedHalfSizes)
        {
            if (TryMatchScaled(known.Width, known.Height, width, height, out nativeW, out nativeH))
                return true;
        }

        nativeW = width;
        nativeH = height;
        return false;
    }

    /// <summary>
    /// True when <paramref name="width"/>×<paramref name="height"/> is the scaled form of a known native size.
    /// </summary>
    public static bool TryUnscaleWithScale(int width, int height, float scale, out int nativeW, out int nativeH)
    {
        if (scale <= 1.001f)
        {
            nativeW = width;
            nativeH = height;
            return false;
        }

        foreach (var known in PrimarySizes)
        {
            if (PreviewScale.ScaleDimension(known.Width, scale) == width
                && PreviewScale.ScaleDimension(known.Height, scale) == height)
            {
                nativeW = known.Width;
                nativeH = known.Height;
                return true;
            }

            if (PreviewScale.ScaleDimension(known.Height, scale) == width
                && PreviewScale.ScaleDimension(known.Width, scale) == height)
            {
                nativeW = known.Height;
                nativeH = known.Width;
                return true;
            }
        }

        foreach (var known in RelatedHalfSizes)
        {
            if (PreviewScale.ScaleDimension(known.Width, scale) == width
                && PreviewScale.ScaleDimension(known.Height, scale) == height)
            {
                nativeW = known.Width;
                nativeH = known.Height;
                return true;
            }
        }

        nativeW = width;
        nativeH = height;
        return false;
    }

    private static bool TryMatchScaled(int knownW, int knownH, int width, int height, out int nativeW, out int nativeH)
    {
        nativeW = knownW;
        nativeH = knownH;

        if (MatchesScaled(knownW, knownH, width, height))
            return true;

        if (MatchesScaled(knownH, knownW, width, height))
        {
            nativeW = knownH;
            nativeH = knownW;
            return true;
        }

        return false;
    }

    private static bool MatchesScaled(int nativeW, int nativeH, int width, int height)
    {
        if (width < nativeW || height < nativeH)
            return false;
        if (width == nativeW && height == nativeH)
            return false;

        var sx = (float)width / nativeW;
        var sy = (float)height / nativeH;
        if (MathF.Abs(sx - sy) > 0.03f)
            return false;
        if (sx <= 1.001f || sx > PreviewScale.MaxScale + 0.05f)
            return false;

        return PreviewScale.ScaleDimension(nativeW, sx) == width
               && PreviewScale.ScaleDimension(nativeH, sx) == height;
    }
}
