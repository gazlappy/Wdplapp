using System;
using Microsoft.Maui.Controls;

namespace Wdpl2.Views;

/// <summary>
/// Placeholder page for importing historical competition results
/// </summary>
public partial class ImportHistoricalCompetitionsPage : ContentPage
{
    public ImportHistoricalCompetitionsPage()
    {
        InitializeComponent();
    }

    public ImportHistoricalCompetitionsPage(Guid seasonId) : this()
    {
        // Pre-select the season (placeholder)
    }
}
