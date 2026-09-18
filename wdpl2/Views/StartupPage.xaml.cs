namespace Wdpl2.Views;

/// <summary>
/// What the app shows while it is opening the league.
/// </summary>
/// <remarks>
/// Opening a season's worth of pool takes a few seconds: a database to bring
/// up, twenty megabytes of league to read, and a copy of it to keep in step.
/// That work used to happen before the first window existed, which on Windows
/// looked like a slow start and on Android was fatal - the phone kills an app
/// that has not finished starting, and this one never had.
/// <para>
/// So there is a window from the first moment, and it says what is happening.
/// </para>
/// </remarks>
public partial class StartupPage : ContentPage
{
    public StartupPage()
    {
        InitializeComponent();
    }

    /// <summary>Says what is being done, for a wait long enough to wonder about.</summary>
    public void Report(string message)
    {
        StatusLabel.Text = message;
    }

    /// <summary>
    /// Says that it did not work, and stops pretending to be busy.
    /// </summary>
    /// <remarks>
    /// A spinner that never stops is the worst of both: nothing has happened
    /// and nothing has said so.
    /// </remarks>
    public void Failed(string message)
    {
        Spinner.IsRunning = false;
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb("#EF4444");
    }
}
