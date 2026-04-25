using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Views.WebsiteBuilder;

namespace Wdpl2.Views
{
    public partial class WebsiteBuilderPage : ContentPage
    {
        private bool _hasNavigated;

        public WebsiteBuilderPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Only navigate once
            if (_hasNavigated) return;
            _hasNavigated = true;
            
            // Small delay to ensure navigation stack is ready
            await Task.Delay(50);
            
            // Navigate to the hub and remove this page from the stack
            var hub = new WebsiteBuilderHub();
            
            // Use MainThread to ensure we're on the UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Push the hub first
                    await Navigation.PushAsync(hub, false);
                    
                    // Then remove this page from the stack
                    Navigation.RemovePage(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                }
            });
        }
    }
}
