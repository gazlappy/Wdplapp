using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public class DatabaseInspectorPage : ContentPage
    {
        private readonly Editor _outputEditor;
        private readonly Button _selectButton;
        private readonly Button _inspectButton;
        private readonly Button _copyButton;
        private readonly ActivityIndicator _loading;
        private string? _selectedFile;

        public DatabaseInspectorPage()
        {
            Title = "Database Inspector";

            _outputEditor = new Editor
            {
                IsReadOnly = true,
                FontFamily = "Courier New",
                FontSize = 11,
                HeightRequest = 500
            };

            _selectButton = new Button { Text = "Select Database File" };
            _inspectButton = new Button { Text = "Inspect Schema", IsEnabled = false };
            _copyButton = new Button { Text = "Copy to Clipboard", IsEnabled = false };
            _loading = new ActivityIndicator { IsRunning = false };

            _selectButton.Clicked += OnSelectFile;
            _inspectButton.Clicked += OnInspect;
            _copyButton.Clicked += OnCopy;

            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 10,
                    Children =
                    {
                        _selectButton,
                        _inspectButton,
                        _loading,
                        _copyButton,
                        _outputEditor
                    }
                }
            };
        }

        private async void OnSelectFile(object? sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Access Database"
                });

                if (result != null)
                {
                    _selectedFile = result.FullPath;
                    _selectButton.Text = $"Selected: {Path.GetFileName(result.FullPath)}";
                    _inspectButton.IsEnabled = true;
                    _outputEditor.Text = "Ready to inspect. Click 'Inspect Schema' button.";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnInspect(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFile)) return;

            try
            {
                _loading.IsRunning = true;
                _inspectButton.IsEnabled = false;

                var result = await Task.Run(() => 
                    AccessDatabaseImporter.InspectDatabaseSchema(_selectedFile));

                _outputEditor.Text = result;
                _copyButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                _outputEditor.Text = $"ERROR:\n{ex}";
            }
            finally
            {
                _loading.IsRunning = false;
                _inspectButton.IsEnabled = true;
            }
        }

        private async void OnCopy(object? sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(_outputEditor.Text);
            await DisplayAlert("Copied", "Schema information copied to clipboard!", "OK");
        }
    }
}