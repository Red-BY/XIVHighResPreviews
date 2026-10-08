using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;

namespace XIVHighResPreviews;

/// <summary>
/// Tracks when character-preview UIs open/close so we can apply upscaling and
/// enable viewport hooks only while they're relevant.
/// </summary>
public sealed class PreviewAddonMonitor : IDisposable
{
    /// <summary>Addons that drive CharaView / offscreen portrait rendering.</summary>
    public static readonly string[] PreviewAddonNames =
    [
        "Character",
        "PvPCharacter",
        "CharacterInspect",
        "CharCard",
        "CharaCard",
        "RetainerCharacter",
        "MiragePrismMiragePlate",
        "Tryon",
        "GearSetPreview",
        "ColorantColoring",
        "BannerEditor",
        "BannerParty",
        "FittingShop",
        "Fashion",
        "InventoryBuddy",
        "InventoryBuddy2",
    ];

    private readonly IAddonLifecycle addonLifecycle;
    private readonly HashSet<string> openAddons = new(StringComparer.Ordinal);

    public PreviewAddonMonitor(IAddonLifecycle addonLifecycle)
    {
        this.addonLifecycle = addonLifecycle;

        addonLifecycle.RegisterListener(AddonEvent.PostSetup, PreviewAddonNames, OnPreviewSetup);
        addonLifecycle.RegisterListener(AddonEvent.PreFinalize, PreviewAddonNames, OnPreviewFinalize);
    }

    public int OpenCount => openAddons.Count;
    public bool AnyOpen => openAddons.Count > 0;

    /// <summary>Fired when a preview addon finishes setup (open or recreate).</summary>
    public event Action<string>? PreviewOpened;

    /// <summary>Fired when the last tracked preview addon closes.</summary>
    public event Action? AllPreviewsClosed;

    public void Dispose()
    {
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, PreviewAddonNames, OnPreviewSetup);
        addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, PreviewAddonNames, OnPreviewFinalize);
    }

    private void OnPreviewSetup(AddonEvent type, AddonArgs args)
    {
        openAddons.Add(args.AddonName);
        PreviewOpened?.Invoke(args.AddonName);
    }

    private void OnPreviewFinalize(AddonEvent type, AddonArgs args)
    {
        openAddons.Remove(args.AddonName);
        if (openAddons.Count == 0)
            AllPreviewsClosed?.Invoke();
    }
}
