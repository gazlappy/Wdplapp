using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;

namespace Wdpl2.Views.Controls
{
    /// <summary>
    /// Small box with a button to import a CSV file (cross-platform).
    /// Raises ImportRequested(Stream stream, string fileName).
    /// </summary>
    public sealed class CsvImportBox : ContentView
    {
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(CsvImportBox), "Import CSV");

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public event Func<Stream, string, Task>? ImportRequested;

        private readonly Label _hint;

        public CsvImportBox()
        {
            var pickBtn = new Button { Text = "Pick CSV" };
            pickBtn.Clicked += OnPickClicked;

            var titleLbl = new Label { FontAttributes = FontAttributes.Bold };
            titleLbl.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));

            _hint = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray,
                Text = "Choose a .csv file"
            };

            Content = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = 12,
                Stroke = Colors.Gray,
                StrokeThickness = 1,
                BackgroundColor = new Color(0.97f, 0.97f, 0.97f),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children = { titleLbl, _hint, pickBtn }
                }
            };
        }

        private static readonly FilePickerFileType CsvFileType =
            new FilePickerFileType(new System.Collections.Generic.Dictionary<DevicePlatform, System.Collections.Generic.IEnumerable<string>>
            {
                { DevicePlatform.iOS,        new[] { "public.comma-separated-values-text" } },
                { DevicePlatform.MacCatalyst, new[] { "public.comma-separated-values-text" } },
                { DevicePlatform.Android,    new[] { "text/csv", "text/comma-separated-values", "application/csv" } },
                { DevicePlatform.WinUI,      new[] { ".csv" } },
                { DevicePlatform.Tizen,      new[] { ".csv" } },
            });

        private async void OnPickClicked(object? sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose a CSV file",
                    FileTypes = CsvFileType
                });
                if (result == null) return;

                await using var stream = await result.OpenReadAsync();
                _hint.Text = $"Importing {result.FileName}…";
                if (ImportRequested != null)
                    await ImportRequested.Invoke(stream, result.FileName);
                _hint.Text = "Choose a .csv file";
            }
            catch (Exception ex)
            {
                // Use Shell.Current for modern MAUI approach
                if (Shell.Current?.CurrentPage != null)
                    await Shell.Current.CurrentPage.DisplayAlert("Import failed", ex.Message, "OK");
                _hint.Text = "Choose a .csv file";
            }
        }
    }
}
