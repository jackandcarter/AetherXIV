using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal sealed class UmbraNotificationService
{
    private readonly Queue<(string Message, UmbraTextTone Tone)> pending = new();
    internal IUmbraNotificationService CreateScope(string pluginId) => new Scope(this, pluginId);
    internal void PublishFrame()
    {
        lock (pending)
        {
            if (!pending.TryDequeue(out var notification)) return;
            try { UmbraNativeUi.PostNotification(notification.Message, (int)notification.Tone); }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }
    }
    private sealed class Scope(UmbraNotificationService owner, string pluginId) : IUmbraNotificationService
    {
        public bool Post(string message, UmbraTextTone tone = UmbraTextTone.Normal)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            string text = $"{pluginId}: {message.Replace('\n', ' ').Replace('\r', ' ')}";
            if (text.Length > 256) text = text[..256];
            lock (owner.pending)
            {
                if (owner.pending.Count >= 32) return false;
                owner.pending.Enqueue((text, tone));
                return true;
            }
        }
    }
}
