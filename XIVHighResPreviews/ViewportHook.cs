using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace XIVHighResPreviews;

/// <summary>
/// Scales viewports that still use the pre-upscale preview size.
/// Without this, the game renders into a corner of a larger RT.
/// Hooks are disabled while upscaling is inactive or no preview is in play.
/// </summary>
public sealed unsafe class ViewportHook : IDisposable
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private readonly Hook<SetViewportDelegate> setViewportHook;
    private readonly Hook<SetMultiViewportsDelegate> setMultiViewportsHook;

    private volatile bool wanted;
    private bool disposed;

    private delegate void SetViewportDelegate(ImmediateContext* ctx, IntRectangle* viewportRectangle, float minDepth, float maxDepth);
    private delegate void SetMultiViewportsDelegate(ImmediateContext* ctx, uint viewportCount, IntRectangle* viewports, float* minDepths, float* maxDepths);

    public ViewportHook(IGameInteropProvider interop, Configuration configuration, IPluginLog log)
    {
        this.configuration = configuration;
        this.log = log;

        setViewportHook = interop.HookFromAddress<SetViewportDelegate>(
            (nint)ImmediateContext.MemberFunctionPointers.SetViewport,
            SetViewportDetour);

        setMultiViewportsHook = interop.HookFromAddress<SetMultiViewportsDelegate>(
            (nint)ImmediateContext.MemberFunctionPointers.SetMultiViewports,
            SetMultiViewportsDetour);

        // Start disabled — Plugin enables when upscaling + preview activity requires it.
        log.Information("Prepared ImmediateContext.SetViewport / SetMultiViewports hooks (disabled until needed).");
    }

    public int ViewportScaleCount { get; private set; }
    public bool IsEnabled => setViewportHook.IsEnabled;

    /// <summary>
    /// Enable or disable the hot viewport detours. Call when upscaling activity changes.
    /// </summary>
    public void SetWanted(bool enable)
    {
        if (disposed)
            return;

        wanted = enable;
        if (enable)
        {
            if (!setViewportHook.IsEnabled)
                setViewportHook.Enable();
            if (!setMultiViewportsHook.IsEnabled)
                setMultiViewportsHook.Enable();
        }
        else
        {
            if (setViewportHook.IsEnabled)
                setViewportHook.Disable();
            if (setMultiViewportsHook.IsEnabled)
                setMultiViewportsHook.Disable();
        }
    }

    public void ResetCounts() => ViewportScaleCount = 0;

    public void Dispose()
    {
        disposed = true;
        wanted = false;
        setViewportHook.Dispose();
        setMultiViewportsHook.Dispose();
    }

    private void SetViewportDetour(ImmediateContext* ctx, IntRectangle* viewportRectangle, float minDepth, float maxDepth)
    {
        if (wanted && viewportRectangle != null && configuration.ScalePreviewViewports && PreviewScale.IsActive(configuration))
        {
            var rect = *viewportRectangle;
            if (PreviewScale.TryScalePreviewRect(ref rect, configuration, out var label))
            {
                ViewportScaleCount++;
                if (configuration.LogPreviewTextureCreates)
                {
                    log.Debug(
                        "SetViewport scaled [{Label}]: {L},{T}-{R},{B} -> {L2},{T2}-{R2},{B2}",
                        label,
                        viewportRectangle->Left,
                        viewportRectangle->Top,
                        viewportRectangle->Right,
                        viewportRectangle->Bottom,
                        rect.Left,
                        rect.Top,
                        rect.Right,
                        rect.Bottom);
                }

                *viewportRectangle = rect;
            }
        }

        setViewportHook.Original(ctx, viewportRectangle, minDepth, maxDepth);
    }

    private void SetMultiViewportsDetour(
        ImmediateContext* ctx,
        uint viewportCount,
        IntRectangle* viewports,
        float* minDepths,
        float* maxDepths)
    {
        if (wanted && viewports != null && configuration.ScalePreviewViewports && PreviewScale.IsActive(configuration))
        {
            for (var i = 0; i < viewportCount; i++)
            {
                var rect = viewports[i];
                if (PreviewScale.TryScalePreviewRect(ref rect, configuration, out _))
                {
                    viewports[i] = rect;
                    ViewportScaleCount++;
                }
            }
        }

        setMultiViewportsHook.Original(ctx, viewportCount, viewports, minDepths, maxDepths);
    }
}
