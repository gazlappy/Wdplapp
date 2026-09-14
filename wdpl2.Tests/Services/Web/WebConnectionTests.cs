using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// The admin password is sent on every authenticated call, so endpoint
/// resolution is a security boundary: it must refuse anything that could send
/// credentials somewhere unintended, rather than "fixing up" a bad address.
/// </summary>
public class WebConnectionTests
{
    private static WebConnection With(string baseUrl) => new() { BaseUrl = baseUrl };

    [Fact]
    public void ResolveEndpoint_AppendsIndexPhp()
    {
        Assert.Equal("https://wdpl.uk/api/index.php", With("https://wdpl.uk/api/").ResolveEndpoint().AbsoluteUri);
    }

    [Fact]
    public void ResolveEndpoint_ToleratesAMissingTrailingSlash()
    {
        Assert.Equal("https://wdpl.uk/api/index.php", With("https://wdpl.uk/api").ResolveEndpoint().AbsoluteUri);
    }

    [Fact]
    public void ResolveEndpoint_TrimsSurroundingWhitespace()
    {
        Assert.Equal("https://wdpl.uk/api/index.php", With("  https://wdpl.uk/api/  ").ResolveEndpoint().AbsoluteUri);
    }

    [Theory]
    [InlineData("http://wdpl.uk/api/")]          // plaintext would expose the password
    [InlineData("ftp://wdpl.uk/api/")]
    public void ResolveEndpoint_RequiresHttps(string url)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => With(url).ResolveEndpoint());
        Assert.Contains("HTTPS", ex.Message);
    }

    [Fact]
    public void ResolveEndpoint_RejectsCredentialsInTheUrl()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => With("https://user:pw@wdpl.uk/api/").ResolveEndpoint());
        Assert.Contains("credentials", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://wdpl.uk/api/?token=abc")]
    [InlineData("https://wdpl.uk/api/#section")]
    public void ResolveEndpoint_RejectsAQueryOrFragment(string url)
    {
        Assert.Throws<InvalidOperationException>(() => With(url).ResolveEndpoint());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void ResolveEndpoint_RejectsEmptyOrMalformedInput(string url)
    {
        Assert.Throws<InvalidOperationException>(() => With(url).ResolveEndpoint());
    }

    [Fact]
    public void IsConfigured_RequiresUrlUserAndPassword()
    {
        Assert.False(new WebConnection { BaseUrl = "https://wdpl.uk/api/" }.IsConfigured);
        Assert.False(new WebConnection { BaseUrl = "https://wdpl.uk/api/", AdminUser = "admin" }.IsConfigured);

        Assert.True(new WebConnection
        {
            BaseUrl = "https://wdpl.uk/api/",
            AdminUser = "admin",
            AdminPassword = "pw",
        }.IsConfigured);
    }
}
