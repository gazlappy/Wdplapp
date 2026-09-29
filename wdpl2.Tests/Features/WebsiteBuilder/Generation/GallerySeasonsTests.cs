using System.Text.RegularExpressions;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// The website gallery, sorted into seasons.
/// </summary>
public class GallerySeasonsTests
{
    private static readonly Season Old = new() { Name = "Winter 24-25", StartDate = new DateTime(2024, 9, 1) };
    private static readonly Season Last = new() { Name = "Winter 25-26", StartDate = new DateTime(2025, 9, 1) };
    private static readonly Season Now = new() { Name = "Winter 26-27", StartDate = new DateTime(2026, 9, 17), IsActive = true };

    private static GalleryImage Photo(string name, Season? season, int order = 0, string caption = "") => new()
    {
        FileName = name + ".jpg",
        Caption = caption,
        SeasonId = season?.Id,
        SortOrder = order,
        StoredFile = name + ".jpg",
    };

    [Fact]
    public void Albums_come_newest_season_first_with_other_photos_last()
    {
        var albums = GalleryAlbums.For(
            new[] { Photo("a", Old), Photo("b", null), Photo("c", Now), Photo("d", Last) },
            new[] { Old, Last, Now });

        Assert.Equal(new[] { "Winter 26-27", "Winter 25-26", "Winter 24-25", "Other photos" },
            albums.Select(a => a.Title));
        Assert.Equal(GalleryAlbums.OtherKey, albums[^1].Key);
    }

    [Fact]
    public void A_season_with_no_photos_gets_no_album()
    {
        var albums = GalleryAlbums.For(new[] { Photo("a", Now) }, new[] { Old, Last, Now });
        Assert.Equal("Winter 26-27", Assert.Single(albums).Title);
    }

    [Fact]
    public void A_photo_whose_season_was_deleted_goes_to_other_photos()
    {
        var gone = new Season { Name = "Deleted" };
        var albums = GalleryAlbums.For(new[] { Photo("a", gone) }, new[] { Now });
        Assert.Equal("Other photos", Assert.Single(albums).Title);
    }

    [Fact]
    public void Photos_keep_their_order_inside_a_season()
    {
        var albums = GalleryAlbums.For(
            new[] { Photo("second", Now, 2), Photo("first", Now, 1), Photo("third", Now, 3) },
            new[] { Now });
        Assert.Equal(new[] { "first.jpg", "second.jpg", "third.jpg" }, albums[0].Images.Select(i => i.FileName));
    }

    private static string GalleryPage(params GalleryImage[] photos)
    {
        var league = new LeagueData { Seasons = { Old, Last, Now } };
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            ShowGallery = true,
            GalleryShowCategories = true,
            GalleryEnableLightbox = true,
            GalleryShowCaptions = true,
        };
        settings.GalleryImages.AddRange(photos);
        return new WebsiteGenerator(league, settings).GenerateWebsite()["gallery.html"];
    }

    [Fact]
    public void The_page_has_a_button_and_a_section_per_season_in_the_same_order()
    {
        var page = GalleryPage(Photo("a", Old), Photo("b", Now), Photo("c", Now), Photo("d", null));

        var buttons = Regex.Matches(page, @"class=""category-btn[^""]*"" data-category=""([^""]+)""")
            .Select(m => m.Groups[1].Value).ToList();
        var sections = Regex.Matches(page, @"<section class=""gallery-season"" data-category=""([^""]+)""")
            .Select(m => m.Groups[1].Value).ToList();

        Assert.Equal("all", buttons[0]);
        Assert.Equal(sections, buttons.Skip(1));
        Assert.Equal(new[] { Now.Id.ToString("N"), Old.Id.ToString("N"), "other" }, sections);
        Assert.Contains("Winter 26-27 <span class=\"category-count\">2</span>", page);
    }

    [Fact]
    public void One_season_needs_no_buttons_or_headings()
    {
        var page = GalleryPage(Photo("a", Now), Photo("b", Now));

        Assert.DoesNotContain("class=\"category-btn", page);
        Assert.DoesNotContain("class=\"gallery-season-title\"", page);
        Assert.Contains("class=\"gallery-season\"", page);
    }

    [Fact]
    public void The_buttons_and_the_viewer_have_a_script_behind_them()
    {
        var page = GalleryPage(Photo("a", Old), Photo("b", Now));

        // The buttons were drawn before with nothing to make them work.
        Assert.Contains("querySelectorAll('.category-btn')", page);
        Assert.Contains("class=\"gallery-viewer\" hidden", page);
        Assert.Contains("closest('.lightbox-link')", page);
    }

    [Fact]
    public void Captions_are_escaped()
    {
        var page = GalleryPage(Photo("a", Now, caption: "Tom & Jerry <b>win</b>"));

        Assert.Contains("Tom &amp; Jerry &lt;b&gt;win&lt;/b&gt;", page);
        Assert.DoesNotContain("<b>win</b>", page);
    }

    [Fact]
    public void Photos_are_linked_as_files_not_written_into_the_page()
    {
        var page = GalleryPage(Photo("finals", Now, caption: "Finals"));

        Assert.Contains("src=\"gallery/thumbs/finals.jpg\"", page);
        Assert.Contains("href=\"gallery/full/finals.jpg\"", page);
        Assert.DoesNotContain("data:image", page);
    }

    [Fact]
    public void A_photo_not_yet_stored_is_left_off_the_page()
    {
        var waiting = Photo("waiting", Now);
        waiting.StoredFile = "";
        var page = GalleryPage(Photo("ready", Now), waiting);

        Assert.Contains("gallery/thumbs/ready.jpg", page);
        Assert.DoesNotContain("waiting", page);
    }
}
