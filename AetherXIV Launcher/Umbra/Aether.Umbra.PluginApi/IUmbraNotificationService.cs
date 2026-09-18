namespace Aether.Umbra.PluginApi;

/// <summary>API 2.1: sends brief status messages through Umbra's shared notification display.</summary>
public interface IUmbraNotificationService
{
    /// <summary>Returns false when the bounded queue is full. Display follows the user's notification setting.</summary>
    bool Post(string message, UmbraTextTone tone = UmbraTextTone.Normal);
}
