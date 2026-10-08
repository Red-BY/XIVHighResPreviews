using System;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.Interop;

namespace XIVHighResPreviews;

/// <summary>
/// Replaces live RTM CharaView render targets that still have native preview sizes
/// with scaled copies. Character RTs are typically allocated at boot; Adventurer Plate
/// and other previews share the same CharaView slot machinery, so this path covers them.
/// </summary>
public sealed unsafe class LiveCharaViewUpscaler
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;

    public LiveCharaViewUpscaler(Configuration configuration, IPluginLog log)
    {
        this.configuration = configuration;
        this.log = log;
    }

    public int LastReplacedCount { get; private set; }
    public float? LastAppliedScale { get; private set; }
    public string LastStatus { get; internal set; } = "Not run yet.";

    /// <summary>
    /// True after a successful apply/check when no native-sized CharaView RTs remain
    /// at the configured scale. Cleared by <see cref="MarkNeedsRescan"/>.
    /// </summary>
    public bool IsSatisfied { get; private set; }

    /// <summary>Forget satisfied state so the next scan/apply can run again.</summary>
    public void MarkNeedsRescan() => IsSatisfied = false;

    /// <summary>Stop fallback polling — nothing left to upscale at the current scale.</summary>
    public void MarkSatisfied() => IsSatisfied = true;

    /// <summary>
    /// True when scaling is active and at least one live CharaView render target
    /// still has a known native preview size.
    /// </summary>
    public bool NeedsApply()
    {
        if (!PreviewScale.IsActive(configuration))
            return false;

        var scale = configuration.PreviewResolutionScale;
        var rtm = RenderTargetManager.Instance();
        if (rtm == null)
            return false;

        return SpanNeedsUpscale(rtm->CharaViewTextures, scale)
               || SpanNeedsUpscale(rtm->CharaViewGBuffers, scale)
               || SpanNeedsUpscale(rtm->CharaViewSemitransparentGBuffers, scale)
               || FieldNeedsUpscale(*(Texture**)((byte*)rtm + 0x358), scale)
               || FieldNeedsUpscale(*(Texture**)((byte*)rtm + 0x360), scale)
               || FieldNeedsUpscale(*(Texture**)((byte*)rtm + 0x368), scale)
               || FieldNeedsUpscale(*(Texture**)((byte*)rtm + 0x3C8), scale)
               || FieldNeedsUpscale(*(Texture**)((byte*)rtm + 0x3E0), scale);
    }

    /// <summary>
    /// True if any live CharaView RT looks like a scaled known preview size
    /// (viewport hooks still matter even with preview UI closed).
    /// </summary>
    public bool HasScaledLiveTextures()
    {
        var scale = LastAppliedScale is > 1.001f
            ? LastAppliedScale.Value
            : configuration.PreviewResolutionScale;
        if (scale <= 1.001f)
            return false;

        var rtm = RenderTargetManager.Instance();
        if (rtm == null)
            return false;

        return SpanHasScaled(rtm->CharaViewTextures, scale)
               || SpanHasScaled(rtm->CharaViewGBuffers, scale)
               || SpanHasScaled(rtm->CharaViewSemitransparentGBuffers, scale);
    }

    /// <summary>
    /// Restore any previously scaled CharaView RTs back to their native sizes,
    /// then apply the current configured scale (if upscaling is still active).
    /// </summary>
    public void ResetAndReapply()
    {
        MarkNeedsRescan();
        var reset = ResetToNative();
        if (!PreviewScale.IsActive(configuration))
        {
            IsSatisfied = true;
            LastStatus = reset == 0
                ? "Upscaling off — already at native sizes (or no live CharaView RTs)."
                : $"Upscaling off — reset {reset} texture(s) to native.";
            log.Information("Live CharaView reset (no reapply): {Status}", LastStatus);
            return;
        }

        Apply();
        if (reset > 0)
            LastStatus = $"Reset {reset}, then {LastStatus}";
    }

    /// <summary>
    /// Replace scaled CharaView RTs with native-sized ones (game will re-render into them).
    /// </summary>
    public int ResetToNative()
    {
        var rtm = RenderTargetManager.Instance();
        if (rtm == null)
            return 0;

        var reset = 0;
        reset += ResetSpan(rtm->CharaViewTextures, "CharaViewTextures");
        reset += ResetSpan(rtm->CharaViewGBuffers, "CharaViewGBuffers");
        reset += ResetSpan(rtm->CharaViewSemitransparentGBuffers, "CharaViewSemitransparentGBuffers");
        reset += ResetField((Texture**)((byte*)rtm + 0x358), "CharaViewViewPosition");
        reset += ResetField((Texture**)((byte*)rtm + 0x360), "CharaViewDepth0");
        reset += ResetField((Texture**)((byte*)rtm + 0x368), "CharaViewDepth1");
        reset += ResetField((Texture**)((byte*)rtm + 0x3C8), "CharaViewPortraitAux");
        reset += ResetField((Texture**)((byte*)rtm + 0x3E0), "CharaViewCompositeMask");

        if (reset > 0)
            LastAppliedScale = null;

        return reset;
    }

    public void Apply()
    {
        LastReplacedCount = 0;

        if (!PreviewScale.IsActive(configuration))
        {
            IsSatisfied = true;
            LastStatus = "Scaling inactive (enable it and set scale > 1.0).";
            return;
        }

        var rtm = RenderTargetManager.Instance();
        if (rtm == null)
        {
            LastStatus = "RenderTargetManager is null.";
            return;
        }

        var scale = configuration.PreviewResolutionScale;
        var replaced = 0;

        // Only replace buffers the offscreen path re-renders into each frame.
        // ORM BackgroundTextures are uploaded/static — swapping them for empty RTs
        // blacks out the Character screen backdrop.
        replaced += ReplaceSpan(rtm->CharaViewTextures, scale, "CharaViewTextures");
        replaced += ReplaceSpan(rtm->CharaViewGBuffers, scale, "CharaViewGBuffers");
        replaced += ReplaceSpan(rtm->CharaViewSemitransparentGBuffers, scale, "CharaViewSemitransparentGBuffers");

        replaced += ReplaceField((Texture**)((byte*)rtm + 0x358), scale, "CharaViewViewPosition");
        replaced += ReplaceField((Texture**)((byte*)rtm + 0x360), scale, "CharaViewDepth0");
        replaced += ReplaceField((Texture**)((byte*)rtm + 0x368), scale, "CharaViewDepth1");
        replaced += ReplaceField((Texture**)((byte*)rtm + 0x3C8), scale, "CharaViewPortraitAux");
        replaced += ReplaceField((Texture**)((byte*)rtm + 0x3E0), scale, "CharaViewCompositeMask");

        LastReplacedCount = replaced;
        if (replaced > 0)
            LastAppliedScale = scale;

        // No remaining native-sized targets → stop polling until something invalidates us.
        IsSatisfied = !NeedsApply();

        LastStatus = replaced == 0
            ? "No native-sized CharaView textures found to replace (already scaled, or empty)."
            : $"Replaced {replaced} live texture(s) at {scale:F2}x.";

        if (replaced > 0)
            log.Information("Live CharaView upscale: {Status}", LastStatus);
        else
            log.Debug("Live CharaView upscale: {Status}", LastStatus);
    }

    private bool SpanHasScaled(Span<Pointer<Texture>> span, float scale)
    {
        for (var i = 0; i < span.Length; i++)
        {
            var tex = span[i].Value;
            if (tex == null)
                continue;

            var w = (int)tex->ActualWidth;
            var h = (int)tex->ActualHeight;
            if (PreviewSizes.TryUnscaleWithScale(w, h, scale, out _, out _))
                return true;
        }

        return false;
    }

    private bool SpanNeedsUpscale(Span<Pointer<Texture>> span, float scale)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (FieldNeedsUpscale(span[i].Value, scale))
                return true;
        }

        return false;
    }

    private bool FieldNeedsUpscale(Texture* tex, float scale)
    {
        if (tex == null || IsStaticUpload(tex))
            return false;

        var w = (int)tex->ActualWidth;
        var h = (int)tex->ActualHeight;
        if (!ShouldReplaceNativeSize(w, h, scale, out _, out _))
            return false;

        return !(configuration.SkipScalingColorUnorm && PreviewScale.IsCompressedFormat(tex->TextureFormat));
    }

    private int ReplaceSpan(Span<Pointer<Texture>> span, float scale, string tag)
    {
        var count = 0;
        for (var i = 0; i < span.Length; i++)
        {
            ref var entry = ref span[i];
            count += ReplaceField((Texture**)Unsafe.AsPointer(ref entry), scale, $"{tag}[{i}]");
        }

        return count;
    }

    private int ReplaceField(Texture** slot, float scale, string tag)
    {
        if (slot == null)
            return 0;

        var old = *slot;
        if (!TryCreateSized(old, scale, tag, upscaleFromNative: true, out var neu))
            return 0;

        *slot = neu;
        old->DecRef();
        return 1;
    }

    private int ResetSpan(Span<Pointer<Texture>> span, string tag)
    {
        var count = 0;
        for (var i = 0; i < span.Length; i++)
        {
            ref var entry = ref span[i];
            count += ResetField((Texture**)Unsafe.AsPointer(ref entry), $"{tag}[{i}]");
        }

        return count;
    }

    private int ResetField(Texture** slot, string tag)
    {
        if (slot == null)
            return 0;

        var old = *slot;
        if (!TryCreateSized(old, scale: 1f, tag, upscaleFromNative: false, out var neu))
            return 0;

        *slot = neu;
        old->DecRef();
        return 1;
    }

    /// <param name="upscaleFromNative">
    /// True: only replace known native sizes with scaled copies.
    /// False: only replace sizes that map back to a smaller native size.
    /// </param>
    private bool TryCreateSized(Texture* old, float scale, string tag, bool upscaleFromNative, out Texture* neu)
    {
        neu = null;
        if (old == null)
            return false;

        var w = (int)old->ActualWidth;
        var h = (int)old->ActualHeight;
        int tw;
        int th;

        if (upscaleFromNative)
        {
            if (!ShouldReplaceNativeSize(w, h, scale, out tw, out th))
                return false;
        }
        else
        {
            // Prefer last applied scale, then the slider value (kept while disabled).
            float? preferred = LastAppliedScale is > 1.001f
                ? LastAppliedScale
                : configuration.PreviewResolutionScale > 1.001f
                    ? configuration.PreviewResolutionScale
                    : null;

            if (!PreviewSizes.TryResolveNativeSize(w, h, out var nw, out var nh, preferred))
                return false;
            if (nw == w && nh == h)
                return false;
            tw = nw;
            th = nh;
        }

        if (IsStaticUpload(old))
        {
            log.Debug("Live upscale skip {Tag}: immutable/static upload {W}×{H} flags={Flags:X}", tag, w, h, (uint)old->Flags);
            return false;
        }

        if (configuration.SkipScalingColorUnorm && PreviewScale.IsCompressedFormat(old->TextureFormat))
        {
            log.Debug("Live upscale skip {Tag}: compressed {Format} {W}×{H}", tag, old->TextureFormat, w, h);
            return false;
        }

        var size = stackalloc int[2];
        size[0] = tw;
        size[1] = th;

        var mip = old->MipLevel == 0 ? (byte)1 : old->MipLevel;
        neu = Device.Instance()->CreateTexture2D(size, mip, old->TextureFormat, old->Flags, 12);
        if (neu == null)
        {
            log.Warning("Live texture replace failed {Tag}: {W}×{H} -> {TW}×{TH}", tag, w, h, tw, th);
            return false;
        }

        log.Information(
            "Live texture replace {Tag}: {W}×{H} -> {TW}×{TH} format={Format} flags={Flags:X}",
            tag,
            w,
            h,
            tw,
            th,
            old->TextureFormat,
            (uint)old->Flags);
        return true;
    }

    private static bool ShouldReplaceNativeSize(int width, int height, float scale, out int targetW, out int targetH)
    {
        targetW = width;
        targetH = height;

        if (!PreviewSizes.TryIdentify(width, height, out _) && !(width == 576 && height == 960))
            return false;

        targetW = PreviewScale.ScaleDimension(width, scale);
        targetH = PreviewScale.ScaleDimension(height, scale);
        return targetW != width || targetH != height;
    }

    private static bool IsStaticUpload(Texture* tex)
        => (tex->Flags & TextureFlags.Immutable) != 0
           && (tex->Flags & (TextureFlags.TextureRenderTarget | TextureFlags.TextureDepthStencil)) == 0;
}
