using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using XIVHighResPreviews.Windows;

namespace XIVHighResPreviews;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    private const string CommandName = "/xivhrp";

    public Configuration Configuration { get; init; }
    public CharaViewInspector Inspector { get; } = new();
    public ViewportHook ViewportHook { get; }
    public LiveCharaViewUpscaler LiveUpscaler { get; }
    public PreviewAddonMonitor PreviewAddons { get; }

    public readonly WindowSystem WindowSystem = new("XIVHighResPreviews");
    private MainWindow MainWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }

    private bool pendingLiveApply;
    private bool pendingResetAndReapply;
    private int autoApplyCooldownFrames;
    private int fallbackScanThrottle;
    private int viewportGateThrottle;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        MigrateConfiguration(Configuration);

        ViewportHook = new ViewportHook(GameInteropProvider, Configuration, Log);
        LiveUpscaler = new LiveCharaViewUpscaler(Configuration, Log);
        PreviewAddons = new PreviewAddonMonitor(AddonLifecycle);
        PreviewAddons.PreviewOpened += OnPreviewOpened;
        PreviewAddons.AllPreviewsClosed += OnAllPreviewsClosed;

        MainWindow = new MainWindow(this);
        DebugWindow = new DebugWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DebugWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings. Optional: `/xivhrp apply`, `/xivhrp debug`."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;

        UpdateViewportHookGate();

        Log.Information(
            "XIV High Res Previews loaded. Use /xivhrp. Upscale={Upscale} scale={Scale:F2}x autoLive={Auto}.",
            Configuration.EnablePreviewUpscale,
            Configuration.PreviewResolutionScale,
            Configuration.AutoUpscaleLiveCharaView);
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        PreviewAddons.PreviewOpened -= OnPreviewOpened;
        PreviewAddons.AllPreviewsClosed -= OnAllPreviewsClosed;
        PreviewAddons.Dispose();

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();
        DebugWindow.Dispose();
        ViewportHook.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            LiveUpscaler.MarkNeedsRescan();
            RequestLiveApply();
            return;
        }

        if (trimmed.Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            ToggleDebugUi();
            return;
        }

        MainWindow.Toggle();
    }

    public void RequestLiveApply() => pendingLiveApply = true;

    /// <summary>
    /// Reset live CharaView RTs to native size, then reapply the current scale (if enabled).
    /// Used when the resolution slider changes or upscaling is toggled.
    /// </summary>
    public void RequestResetAndReapply()
    {
        LiveUpscaler.MarkNeedsRescan();
        pendingResetAndReapply = true;
        pendingLiveApply = false;
        UpdateViewportHookGate();
    }

    private void OnPreviewOpened(string addonName)
    {
        Log.Debug("Preview addon opened: {Name}", addonName);
        LiveUpscaler.MarkNeedsRescan();
        UpdateViewportHookGate();

        if (Configuration.EnablePreviewUpscale && Configuration.AutoUpscaleLiveCharaView)
            RequestLiveApply();
    }

    private void OnAllPreviewsClosed()
    {
        UpdateViewportHookGate();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (autoApplyCooldownFrames > 0)
            autoApplyCooldownFrames--;

        // Keep viewport hooks in sync without checking every single frame.
        if (++viewportGateThrottle >= 30)
        {
            viewportGateThrottle = 0;
            UpdateViewportHookGate();
        }

        if (pendingResetAndReapply)
        {
            pendingResetAndReapply = false;
            pendingLiveApply = false;
            try
            {
                LiveUpscaler.ResetAndReapply();
                autoApplyCooldownFrames = 30;
                UpdateViewportHookGate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Live CharaView reset/reapply failed");
                LiveUpscaler.LastStatus = $"Error: {ex.Message}";
                autoApplyCooldownFrames = 120;
            }

            return;
        }

        // Event-driven apply is primary; fallback poll only while unsatisfied
        // (covers missed addon names / late RT allocation after PostSetup).
        if (!pendingLiveApply
            && Configuration.EnablePreviewUpscale
            && Configuration.AutoUpscaleLiveCharaView
            && !LiveUpscaler.IsSatisfied
            && autoApplyCooldownFrames == 0)
        {
            if (++fallbackScanThrottle >= 30)
            {
                fallbackScanThrottle = 0;
                try
                {
                    if (LiveUpscaler.NeedsApply())
                        pendingLiveApply = true;
                    else
                        LiveUpscaler.MarkSatisfied();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Live CharaView needs-apply scan failed");
                    autoApplyCooldownFrames = 60;
                }
            }
        }
        else
        {
            fallbackScanThrottle = 0;
        }

        if (!pendingLiveApply)
            return;

        pendingLiveApply = false;
        try
        {
            LiveUpscaler.Apply();
            // If PostSetup raced ahead of RT allocation, stay unsatisfied briefly.
            if (!LiveUpscaler.IsSatisfied)
                autoApplyCooldownFrames = 15;
            else
                autoApplyCooldownFrames = 30;

            UpdateViewportHookGate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Live CharaView upscale failed");
            LiveUpscaler.LastStatus = $"Error: {ex.Message}";
            autoApplyCooldownFrames = 120;
        }
    }

    /// <summary>
    /// Viewport detours only while upscaling is on and a preview UI is open.
    /// </summary>
    private void UpdateViewportHookGate()
    {
        var want =
            Configuration.ScalePreviewViewports
            && PreviewScale.IsActive(Configuration)
            && PreviewAddons.AnyOpen;

        ViewportHook.SetWanted(want);
    }

    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleDebugUi() => DebugWindow.Toggle();

    private static void MigrateConfiguration(Configuration cfg)
    {
        var dirty = false;

        if (cfg.Version < 6)
        {
            cfg.AutoUpscaleLiveCharaView = true;
            cfg.Version = 6;
            dirty = true;
        }

        if (cfg.Version < 7)
        {
            cfg.Version = 7;
            dirty = true;
        }

        if (cfg.Version < 8)
        {
            // LogPreviewTextureCreates was renamed to LogViewportScales; default stays off.
            cfg.Version = 8;
            dirty = true;
        }

        if (cfg.PreviewResolutionScale < PreviewScale.MinScale)
        {
            cfg.PreviewResolutionScale = PreviewScale.MinScale;
            dirty = true;
        }

        if (dirty)
            cfg.Save();
    }
}
