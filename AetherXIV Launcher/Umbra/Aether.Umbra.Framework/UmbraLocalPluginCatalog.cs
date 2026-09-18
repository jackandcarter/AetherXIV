using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aether.Umbra.Framework;

/// <summary>Stages local packages for Discover using the existing repository and installer pipeline.</summary>
internal static class UmbraLocalPluginCatalog
{
    private const long MaximumBytes = 512L * 1024 * 1024;
    private static readonly string[] ManifestNames = ["umbra-plugin.json", "plugin.json"];

    public static string Create(string location, string cacheDirectory, CancellationToken cancellationToken)
    {
        string input = Path.GetFullPath(location);
        List<string> candidates = [];
        if (Directory.Exists(input) && !ManifestNames.Any(name => File.Exists(Path.Combine(input, name))))
        {
            candidates.AddRange(Directory.EnumerateFiles(input, "*.zip", SearchOption.TopDirectoryOnly));
            candidates.AddRange(Directory.EnumerateDirectories(input).Where(directory =>
                ManifestNames.Any(name => File.Exists(Path.Combine(directory, name)))));
        }
        else candidates.Add(input);
        if (candidates.Count == 0 || candidates.Count > 128)
            throw new InvalidDataException("Select a plugin ZIP, a DLL with its adjacent manifest, a plugin folder, or a folder containing up to 128 plugin packages.");

        string directory = Path.Combine(cacheDirectory, "LocalPackages", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(input) && Path.GetFullPath(directory).StartsWith(input.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a plugin build folder outside Umbra's package cache.");
        Directory.CreateDirectory(directory);
        try
        {
            List<object> entries = [];
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Select the original plugin location rather than a symbolic link.");
                string package = Path.Combine(directory, $"{entries.Count}.zip");
                if (string.Equals(Path.GetExtension(candidate), ".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                {
                    if (new FileInfo(candidate).Length > MaximumBytes) throw new InvalidDataException("Plugin ZIP exceeds 512 MiB.");
                    File.Copy(candidate, package);
                }
                else
                {
                    UmbraPluginManifest manifest = UmbraDeveloperPluginDiscovery.LoadLocation(candidate);
                    string root = Path.GetDirectoryName(manifest.ManifestPath)!;
                    using ZipArchive zip = ZipFile.Open(package, ZipArchiveMode.Create);
                    long total = 0;
                    int count = 0;
                    Stack<string> pending = new(); pending.Push(root);
                    while (pending.TryPop(out string? current))
                    {
                        foreach (string path in Directory.EnumerateFileSystemEntries(current))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            FileAttributes attributes = File.GetAttributes(path);
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                                throw new InvalidDataException("Plugin folders cannot contain symbolic links.");
                            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                            if (++count > 4096 || (total += new FileInfo(path).Length) > MaximumBytes)
                                throw new InvalidDataException("Plugin folder exceeds package limits. Select its build-output folder.");
                            zip.CreateEntryFromFile(path, Path.GetRelativePath(root, path).Replace('\\', '/'), CompressionLevel.Fastest);
                        }
                    }
                }
                using ZipArchive archive = ZipFile.OpenRead(package);
                if (archive.Entries.Count > 4096 || archive.Entries.Sum(entry => entry.Length) > MaximumBytes)
                    throw new InvalidDataException("Plugin archive exceeds package limits.");
                ZipArchiveEntry? manifestEntry = ManifestNames.Select(archive.GetEntry).FirstOrDefault(entry => entry is not null);
                if (manifestEntry is null || manifestEntry.Length > 2 * 1024 * 1024)
                    throw new InvalidDataException("Plugin ZIP needs umbra-plugin.json or plugin.json at its root.");
                using Stream stream = manifestEntry.Open();
                UmbraPluginManifest parsed = JsonSerializer.Deserialize<UmbraPluginManifest>(stream)
                    ?? throw new InvalidDataException("Invalid plugin manifest.");
                parsed.Validate();
                UmbraPluginCompatibility.Validate(parsed);
                if (!ids.Add(parsed.Id)) throw new InvalidDataException($"More than one package declares plugin {parsed.Id}.");
                if (archive.GetEntry(parsed.Entry.Replace('\\', '/')) is null)
                    throw new InvalidDataException($"The package is missing its declared entry: {parsed.Entry}.");
                string validationFile = Path.Combine(directory, "entry-validation.dll");
                try
                {
                    archive.GetEntry(parsed.Entry.Replace('\\', '/'))!.ExtractToFile(validationFile, overwrite: true);
                    UmbraManagedPluginValidator.ValidateEntryAssembly(validationFile);
                }
                finally { if (File.Exists(validationFile)) File.Delete(validationFile); }
                using JsonDocument metadata = JsonDocument.Parse(manifestEntry.Open());
                string? Metadata(string name) => metadata.RootElement.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                string? icon = Metadata("icon_url");
                if (!string.IsNullOrWhiteSpace(icon) && !Uri.TryCreate(icon, UriKind.Absolute, out _))
                {
                    ZipArchiveEntry? iconEntry = archive.GetEntry(icon.Replace('\\', '/'));
                    if (iconEntry is not null && iconEntry.Length is > 0 and <= 4 * 1024 * 1024)
                    {
                        string iconPath = Path.Combine(directory, $"{entries.Count}.icon");
                        iconEntry.ExtractToFile(iconPath);
                        icon = new Uri(iconPath).AbsoluteUri;
                    }
                    else icon = null;
                }
                using FileStream bytes = File.OpenRead(package);
                entries.Add(new
                {
                    id = parsed.Id, name = parsed.Name, version = parsed.Version, api_version = parsed.ApiVersion,
                    entry = parsed.Entry, minimum_framework_version = parsed.MinimumFrameworkVersion,
                    target_framework = parsed.TargetFramework, architecture = parsed.Architecture, language = parsed.Language,
                    download_url = new Uri(package).AbsoluteUri, size_bytes = bytes.Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(bytes)), built_in = false,
                    author = Metadata("author"), description = Metadata("description") ?? "Local development package.",
                    punchline = Metadata("punchline"), icon_url = icon, repo_url = Metadata("repo_url")
                });
            }
            string catalog = Path.Combine(directory, "repository.json");
            File.WriteAllText(catalog, JsonSerializer.Serialize(new
            {
                schema_version = 1, repository_name = $"Local: {Path.GetFileName(input)}", plugins = entries
            }));
            return catalog;
        }
        catch { Directory.Delete(directory, recursive: true); throw; }
    }
}
