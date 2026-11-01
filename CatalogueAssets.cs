using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

[assembly: InternalsVisibleTo("BetterJournal.Tests")]

namespace BetterJournal;

// Catalogue metadata is optional. One stale origin or invalid JSON must not disable the reader.
internal static class CatalogueAssets
{
    internal static IEnumerable<KeyValuePair<AssetLocation, T>> Read<T>(IAssetManager assets,
        string path, Action<string> warn, ISet<string>? domains = null, bool exactPath = false) where T : class
    {
        List<AssetLocation> locations;
        try { locations = assets.GetLocations(path); }
        catch (Exception ex)
        {
            warn($"Could not enumerate optional catalogue assets at {path}: {ex.Message}");
            yield break;
        }

        foreach (var location in locations)
        {
            if (domains != null && !domains.Contains(location.Domain) || exactPath && location.Path != path) continue;
            T? value;
            try { value = assets.Get<T>(location); }
            catch (Exception ex)
            {
                warn($"Skipping optional catalogue asset {location}: {ex.Message}");
                continue;
            }
            if (value != null) yield return new(location, value);
            else warn($"Skipping empty optional catalogue asset {location}.");
        }
    }
}
