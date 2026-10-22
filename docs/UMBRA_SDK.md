# Umbra SDK and first plugin

Umbra 2.1.2 exposes **plugin API 2.1** for managed C# plugins running inside the
Windows x86 Final Fantasy XIV 1.23b client. You can build a plugin on Windows,
macOS, or Linux using the .NET SDK for your development computer.

This quickstart uses the public API DLL already included with Launcher.
The application release does not include a separate downloadable SDK/NuGet
archive, and the API packages are not published on nuget.org. No private
framework source checkout is required.

## Version contract

| Item | Value for this release |
|---|---|
| Umbra framework | `2.1.2` |
| Plugin API compatibility | `2.1` |
| API/SDK package version, when supplied separately | `2.1.2` |
| Development SDK | .NET SDK `10.0.203` |
| Plugin target | `net10.0-windows` |
| Game process | Windows x86, including under Wine |

API version and package version are different fields. Declare `api_version`
as `2.1` and `minimum_framework_version` as `2.1.2` for the examples here.
Use managed IL compatible with the x86 process. An x64-only native dependency
cannot load into the game.

## Obtain the public API

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0),
then extract a matching Launcher or Full archive. Copy
`Aether.Umbra.PluginApi.dll` into a separate development workspace:

| Platform archive | API location inside the extracted package |
|---|---|
| Windows, Linux, SteamOS | `launcher/app/Umbra/Framework/Managed/Aether.Umbra.PluginApi.dll` |
| macOS | `AetherXIV Launcher.app/Contents/Resources/Umbra/Framework/Managed/Aether.Umbra.PluginApi.dll` |

Use a **copy**. Keep the verified Launcher and Umbra payload untouched.
Do not reference `Aether.Umbra.Framework.dll` or the native bootstrap.

Create this directory layout:

```text
PluginWorkspace/
  sdk/
    Aether.Umbra.PluginApi.dll
  HelloUmbra/
    HelloUmbra.csproj
    HelloPlugin.cs
    umbra-plugin.json
```

## Create the project

Save `HelloUmbra/HelloUmbra.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>HelloUmbra</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Aether.Umbra.PluginApi">
      <HintPath>../sdk/Aether.Umbra.PluginApi.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <None Update="umbra-plugin.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="Assets/**/*" CopyToOutputDirectory="PreserveNewest"
          CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

`Private=false` keeps the framework-owned API out of your plugin output. The
framework supplies the compatible assembly when it loads the plugin.

## Implement the plugin

Save `HelloUmbra/HelloPlugin.cs`:

```csharp
using Aether.Umbra.PluginApi;

public sealed class HelloPlugin : IUmbraPlugin, IUmbraPluginUi
{
    private bool open;

    public string Name => "Hello Umbra";

    public void Initialize(IUmbraPluginContext context)
    {
        context.Logger.Info("Hello Umbra initialized.");
    }

    public void OpenMainUi() => open = true;

    public void Update(TimeSpan delta) { }

    public void Draw(IUmbraDrawContext draw)
    {
        if (!open)
            return;

        bool visible = draw.BeginWindow("Hello Umbra###HelloUmbra", ref open);
        try
        {
            if (visible)
                draw.Text("My first Umbra plugin.", UmbraTextTone.Accent);
        }
        finally
        {
            draw.EndWindow();
        }
    }

    public void Dispose() { }
}
```

The Plugin Manager's **Open** action calls `OpenMainUi`. Keep rendering inside
`Draw`, balance every window/child begin and end call, and release registrations,
cancellation sources, media, and other resources in `Dispose`.

## Add the manifest

Save `HelloUmbra/umbra-plugin.json`:

```json
{
  "id": "com.example.hello-umbra",
  "name": "Hello Umbra",
  "version": "1.0.0",
  "api_version": "2.1",
  "entry": "HelloUmbra.dll",
  "entry_type": "HelloPlugin",
  "minimum_framework_version": "2.1.2",
  "target_framework": "net10.0-windows",
  "architecture": "x86",
  "language": "CSharp",
  "enabled": false,
  "capabilities": []
}
```

Choose your own stable plugin ID before distribution. Declare only the
capabilities your plugin needs. Service lookup returns null when the required
capability is missing; a service can also report that its native adapter is
unavailable.

## Build and load locally

From `PluginWorkspace`:

```sh
dotnet build HelloUmbra/HelloUmbra.csproj --configuration Release
```

The output is `HelloUmbra/bin/Release/net10.0-windows/`. In the in-game Plugin
Manager, enable developer-plugin loading and add that output directory as a
developer location. Under Wine, a host path may be reached through the mapped
`Z:` drive. Rescan, enable **Hello Umbra**, and choose **Open**.

Use the Plugin Manager to disable/reload the plugin before replacing a DLL
that is still in use. Developer locations remain owned by you; the manager
does not install, update, or delete their development files.

## Package for installation

Create a ZIP with these files at its root:

```text
HelloUmbra.dll
umbra-plugin.json
```

Include any plugin-owned dependency DLLs and `Assets/` paths your plugin uses.
Do not include `Aether.Umbra.PluginApi.dll`, the framework, a .NET runtime,
the game client, build intermediates, or debug symbols.

For repository installation, publish the ZIP and a compatible HTTPS plugin
index containing its exact byte size and SHA-256. The full
[plugin development reference](umbra/PLUGIN_DEVELOPMENT.md#repositories-and-package-security)
describes the manifest and repository fields.

## MSBuild SDK packages

The separate `Aether.Umbra.Sdk` MSBuild SDK, version 2.1.2, sets the target
framework, API package reference, manifest checks, and nested asset copying.
If you are supplied its approved local NuGet packages, configure the feed before
using `<Project Sdk="Aether.Umbra.Sdk/2.1.2">`. That SDK also requires the matching
`Aether.Umbra.PluginApi` package. A nuget.org-only restore will not find them.

The direct-assembly project above is the working route for this documentation-only
distribution. You do not need to reconstruct the framework or bootstrap to use it.

## Further development

- [API reference, capabilities, lifecycle, packaging and repositories](umbra/PLUGIN_DEVELOPMENT.md).
- [UI images, GIFs and sprite animations](umbra/UI_MEDIA_API.md).
- [Performance snapshots, tracing and messaging](umbra/DIAGNOSTICS.md).
- [Resource loading and appearance availability](umbra/RESOURCE_AND_APPEARANCE.md).
- [Plugin installation and recovery](umbra/USING_UMBRA.md).
