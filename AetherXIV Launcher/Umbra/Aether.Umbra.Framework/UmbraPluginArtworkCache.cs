using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Aether.Umbra.Framework;

/// <summary>Downloads catalog artwork off the render thread. Failures retain the standard fallback.</summary>
internal sealed class UmbraPluginArtworkCache(string cacheDirectory)
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, Task<string?>> requests = new();

    internal string? Get(UmbraStoreEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.IconUrl)) return null;
        var repository = new UmbraRepositorySource(entry.RepositoryUrl, entry.Source);
        Uri icon;
        try { icon = new Uri(repository.ResolveManifestUri(), entry.IconUrl); }
        catch (UriFormatException) { return null; }
        if (!UmbraRepositorySource.IsAllowedUri(icon.AbsoluteUri) || (icon.IsFile && !repository.IsLocalFileSource)) return null;
        if (requests.Count >= 256 && !requests.ContainsKey(icon.AbsoluteUri)) return null;
        Task<string?> request = requests.GetOrAdd(icon.AbsoluteUri, _ => Task.Run(() => Fetch(icon)));
        return request.IsCompletedSuccessfully ? request.Result : null;
    }

    private async Task<string?> Fetch(Uri uri)
    {
        string? temporary = null;
        try
        {
            string directory = Path.Combine(cacheDirectory, "Artwork");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))) + ".image");
            if (File.Exists(path) && new FileInfo(path).Length is > 0 and <= MaximumBytes) return path;
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };
            using HttpResponseMessage? response = uri.IsFile ? null : await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response?.EnsureSuccessStatusCode();
            await using Stream input = uri.IsFile ? File.OpenRead(uri.LocalPath) : await response!.Content.ReadAsStreamAsync().ConfigureAwait(false);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (FileStream output = File.Create(temporary))
            {
                byte[] buffer = new byte[16384];
                int total = 0, count;
                while ((count = await input.ReadAsync(buffer).ConfigureAwait(false)) != 0)
                {
                    total += count;
                    if (total > MaximumBytes) throw new InvalidDataException("Plugin artwork exceeds 4 MiB.");
                    await output.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                }
                if (total == 0) throw new InvalidDataException("Empty plugin artwork.");
            }
            File.Move(temporary, path, overwrite: true);
            return path;
        }
        catch (Exception) { return null; }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }
}
