using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>One season's photos, as the gallery page shows them.</summary>
/// <param name="Key">Stable and safe to use in the page: the season's id, or "other".</param>
public sealed record GalleryAlbum(string Key, string Title, List<GalleryImage> Images);

/// <summary>
/// Sorts the gallery's photos into seasons.
/// </summary>
public static class GalleryAlbums
{
    public const string OtherKey = "other";
    public const string OtherTitle = "Other photos";

    /// <summary>
    /// One album per season that has photos, newest season first, then any
    /// photos with no season (or a season since deleted) as "Other photos".
    /// </summary>
    /// <remarks>
    /// Photos keep their own order inside an album. Seasons with no photos
    /// are left out: an empty heading on the website is just noise.
    /// </remarks>
    public static List<GalleryAlbum> For(IEnumerable<GalleryImage> images, IEnumerable<Season> seasons)
    {
        var bySeason = seasons.ToDictionary(s => s.Id);
        var ordered = images.OrderBy(i => i.SortOrder).ThenBy(i => i.DateAdded).ToList();

        var albums = ordered
            .Where(i => i.SeasonId is Guid id && bySeason.ContainsKey(id))
            .GroupBy(i => i.SeasonId!.Value)
            .Select(g => (Season: bySeason[g.Key], Images: g.ToList()))
            .OrderByDescending(a => a.Season.StartDate)
            .Select(a => new GalleryAlbum(a.Season.Id.ToString("N"), a.Season.Name, a.Images))
            .ToList();

        var other = ordered
            .Where(i => i.SeasonId is not Guid id || !bySeason.ContainsKey(id))
            .ToList();

        if (other.Count > 0)
            albums.Add(new GalleryAlbum(OtherKey, OtherTitle, other));

        return albums;
    }
}
