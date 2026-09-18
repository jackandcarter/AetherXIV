namespace Aether.Umbra.PluginApi;

/// <summary>Optional API 2.1 entry point for a plugin's main window.</summary>
public interface IUmbraPluginUi
{
    void OpenMainUi();
}

/// <summary>Optional API 2.1 entry point for a plugin's settings window.</summary>
public interface IUmbraPluginSettingsUi
{
    void OpenSettingsUi();
}
