using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using WorkScedulerApp.Data;

namespace WorkScedulerApp.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    [ObservableProperty]
    private List<string> _locationPaths;
    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;
        
        // TEMP: Remove
        LocationPaths =
        [
            @"C:Users\Angel\Downloads\Test",
            @"C:Users\Angel\Downloads\Templates"
        ];
    }
}