using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Wdpl2.Tests;

public class SmartImportLayoutTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Fact]
    public void ScanProgress_IsOutsideScrollViewInBoundedRootRow()
    {
        var document = LoadPage();
        var panel = Named(document, "ScanProgressPanel");
        var wizard = Named(document, "WizardScrollView");
        Assert.Same(document.Root!.Elements().Single(), panel.Parent);
        Assert.Same(panel.Parent, wizard.Parent);
        Assert.Equal("Auto,*,Auto", (string?)panel.Parent!.Attribute("RowDefinitions"));
        Assert.Equal("1", (string?)panel.Attribute("Grid.Row"));
        Assert.DoesNotContain(panel.Ancestors(), e => e.Name.LocalName == "ScrollView");
        Assert.Equal("Grid", panel.Elements().Single().Name.LocalName);
    }

    [Fact]
    public void ScanSpinner_HasExplicitDimensionsAndStartsStopped()
    {
        var spinner = Named(LoadPage(), "ScanSpinner");
        Assert.Equal("40", (string?)spinner.Attribute("WidthRequest"));
        Assert.Equal("40", (string?)spinner.Attribute("HeightRequest"));
        Assert.Equal("Center", (string?)spinner.Attribute("HorizontalOptions"));
        Assert.Equal("False", (string?)spinner.Attribute("IsRunning"));
    }

    [Theory]
    [InlineData("ScanProgressLabel")]
    [InlineData("ScanCountLabel")]
    public void ScanLabels_StaySingleLineWithinProgressGrid(string name)
    {
        var document = LoadPage();
        var label = Named(document, name);
        Assert.Equal("1", (string?)label.Attribute("MaxLines"));
        Assert.Equal("Fill", (string?)label.Attribute("HorizontalOptions"));
        Assert.Contains(label.Ancestors(), e => e == Named(document, "ScanProgressPanel"));
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e => (string?)e.Attribute(Xaml + "Name") == name);

    private static XDocument LoadPage([CallerFilePath] string sourceFile = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        return XDocument.Load(Path.Combine(root, "wdpl2", "Views", "Import", "SmartImportPage.xaml"));
    }
}
