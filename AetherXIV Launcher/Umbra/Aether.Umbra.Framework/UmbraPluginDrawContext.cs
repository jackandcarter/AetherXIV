using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

/// <summary>
/// Host-owned startup window policy. Draw callbacks still run, but plugin UI
/// cannot reach the renderer until the user opens that plugin for this load.
/// Suppression never changes the plugin's own open flags or input values.
/// </summary>
internal sealed class UmbraPluginDrawContext(
    IUmbraDrawContext inner, bool windowsAllowed, Action windowRequested) : IUmbraDrawContext
{
    public ulong FrameNumber => inner.FrameNumber;
    public TimeSpan DeltaTime => inner.DeltaTime;
    public int ViewportWidth => inner.ViewportWidth;
    public int ViewportHeight => inner.ViewportHeight;
    public int DeviceGeneration => inner.DeviceGeneration;
    public bool IsRenderThread => inner.IsRenderThread;
    public bool IsPluginManagerOpen => inner.IsPluginManagerOpen;
    public float AvailableContentWidth => windowsAllowed ? inner.AvailableContentWidth : 0;
    public float ContentRegionWidth => windowsAllowed ? inner.ContentRegionWidth : 0;
    public void RequestPluginManagerOpen() { if (windowsAllowed) inner.RequestPluginManagerOpen(); }
    public bool BeginWindow(string title, ref bool isOpen)
    {
        windowRequested();
        return windowsAllowed && inner.BeginWindow(title, ref isOpen);
    }
    public void EndWindow() { if (windowsAllowed) inner.EndWindow(); }
    public void Text(string text) { if (windowsAllowed) inner.Text(text); }
    public void Text(string text, UmbraTextTone tone) { if (windowsAllowed) inner.Text(text, tone); }
    public void Text(string text, UmbraTextTone tone, UmbraTextStyle style) { if (windowsAllowed) inner.Text(text, tone, style); }
    public bool InputText(string label, ref string value, string hint = "", int maximumLength = 256) => windowsAllowed && inner.InputText(label, ref value, hint, maximumLength);
    public bool Button(string label) => windowsAllowed && inner.Button(label);
    public bool Button(string label, UmbraButtonStyle style, UmbraIcon icon = UmbraIcon.None, float width = 0, float height = 0) => windowsAllowed && inner.Button(label, style, icon, width, height);
    public bool Checkbox(string label, ref bool value) => windowsAllowed && inner.Checkbox(label, ref value);
    public bool Toggle(string label, ref bool value) => windowsAllowed && inner.Toggle(label, ref value);
    public bool InputInt(string label, ref int value, int step = 1) => windowsAllowed && inner.InputInt(label, ref value, step);
    public bool SliderInt(string label, ref int value, int minimum, int maximum) => windowsAllowed && inner.SliderInt(label, ref value, minimum, maximum);
    public bool SliderFloat(string label, ref float value, float minimum, float maximum) => windowsAllowed && inner.SliderFloat(label, ref value, minimum, maximum);
    public bool Combo(string label, ref int selectedIndex, IReadOnlyList<string> items) => windowsAllowed && inner.Combo(label, ref selectedIndex, items);
    public bool CollapsingHeader(string label, bool defaultOpen = false) => windowsAllowed && inner.CollapsingHeader(label, defaultOpen);
    public void ProgressBar(float fraction, string overlay = "") { if (windowsAllowed) inner.ProgressBar(fraction, overlay); }
    public void SameLine() { if (windowsAllowed) inner.SameLine(); }
    public void Separator() { if (windowsAllowed) inner.Separator(); }
    public void Spacing(float height = 8) { if (windowsAllowed) inner.Spacing(height); }
    public void Icon(UmbraIcon icon, UmbraTextTone tone = UmbraTextTone.Normal, float size = 20) { if (windowsAllowed) inner.Icon(icon, tone, size); }
    public void Badge(string text, UmbraTextTone tone, UmbraIcon icon = UmbraIcon.None) { if (windowsAllowed) inner.Badge(text, tone, icon); }
    public void Artwork(string seed, UmbraIcon icon = UmbraIcon.Plug, float size = 72) { if (windowsAllowed) inner.Artwork(seed, icon, size); }
    public void SetNextWindowSize(float width, float height, bool firstUseOnly = true) { if (windowsAllowed) inner.SetNextWindowSize(width, height, firstUseOnly); }
    public bool BeginChild(string id, float height, bool border = true) => windowsAllowed && inner.BeginChild(id, height, border);
    public bool BeginPanel(string id, float width, float height, UmbraPanelStyle style = UmbraPanelStyle.Card) => windowsAllowed && inner.BeginPanel(id, width, height, style);
    public void EndChild() { if (windowsAllowed) inner.EndChild(); }
}
