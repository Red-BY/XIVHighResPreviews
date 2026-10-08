using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace XIVHighResPreviews;

/// <summary>
/// Read-only probe for CharaView / offscreen character-preview render targets.
/// See README "Research notes" for how these map to in-game UI.
/// </summary>
public sealed unsafe class CharaViewInspector
{
    public const int SlotCount = 8;

    /// <summary>
    /// ClientObjectIndex usage documented in FFXIVClientStructs CharaView:
    /// 0 Character / PvPCharacter;
    /// 1 Inspect / CharaCard / Fashion / RetainerStatus;
    /// 2 TryOn / GearSetPreview;
    /// 3 Colorant;
    /// 4 BannerList / BannerEdit / BannerUpdateView / FittingShop;
    /// 0–7 BannerParty.
    /// </summary>
    public static readonly string[] SlotLabels =
    [
        "0 — Character / PvPCharacter",
        "1 — Inspect / CharaCard / Fashion / Retainer",
        "2 — Try On / Gear Set Preview",
        "3 — Colorant (dye)",
        "4 — Banner / Fitting Shop",
        "5 — BannerParty slot",
        "6 — BannerParty slot",
        "7 — BannerParty slot",
    ];

    public readonly record struct TextureInfo(
        nint Address,
        uint ActualWidth,
        uint ActualHeight,
        uint AllocatedWidth,
        uint AllocatedHeight,
        TextureFormat Format);

    public readonly record struct SlotInfo(
        int Index,
        string Label,
        bool HasTexture,
        TextureInfo? Texture,
        bool HasBackground,
        TextureInfo? Background);

    public readonly record struct Snapshot(
        bool RenderTargetManagerAvailable,
        bool OffscreenManagerAvailable,
        uint MainResolutionWidth,
        uint MainResolutionHeight,
        float GraphicsRezoScale,
        float GraphicsRezoScaleY,
        IReadOnlyList<SlotInfo> Slots);

    public Snapshot Capture()
    {
        var rtm = RenderTargetManager.Instance();
        var orm = OffscreenRenderingManager.Instance();

        var slots = new List<SlotInfo>(SlotCount);

        if (rtm == null)
        {
            for (var i = 0; i < SlotCount; i++)
                slots.Add(new SlotInfo(i, SlotLabels[i], false, null, false, null));

            return new Snapshot(false, orm != null, 0, 0, 0, 0, slots);
        }

        for (var i = 0; i < SlotCount; i++)
        {
            TextureInfo? texInfo = null;
            TextureInfo? bgInfo = null;

            var tex = rtm->GetCharaViewTexture((uint)i);
            if (tex != null)
                texInfo = FromTexture(tex);

            // Fallback / cross-check: span of resulting images on the manager
            if (texInfo == null && i < rtm->CharaViewTextures.Length)
            {
                var ptr = rtm->CharaViewTextures[i].Value;
                if (ptr != null)
                    texInfo = FromTexture(ptr);
            }

            if (orm != null && i < orm->BackgroundTextures.Length)
            {
                var bg = orm->BackgroundTextures[i].Value;
                if (bg != null)
                    bgInfo = FromTexture(bg);
            }

            slots.Add(new SlotInfo(
                i,
                SlotLabels[i],
                texInfo != null,
                texInfo,
                bgInfo != null,
                bgInfo));
        }

        return new Snapshot(
            true,
            orm != null,
            rtm->Resolution_Width,
            rtm->Resolution_Height,
            rtm->GraphicsRezoScale,
            rtm->GraphicsRezoScaleY,
            slots);
    }

    private static TextureInfo FromTexture(Texture* tex) => new(
        (nint)tex,
        tex->ActualWidth,
        tex->ActualHeight,
        tex->AllocatedWidth,
        tex->AllocatedHeight,
        tex->TextureFormat);
}
