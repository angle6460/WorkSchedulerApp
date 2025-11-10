using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using WorkSchedulerApp.Data;

namespace WorkSchedulerApp.ViewModels;

public partial class SettingsPageViewModel : PageViewModel
{
    private readonly string settingsFilePath;
    private readonly string cacheDirectory;

    // ✅ Bindable properties
    [ObservableProperty]
    private bool _skipFiles;

    [ObservableProperty]
    private bool _allowDuplicateEntries;

    [ObservableProperty]
    private ObservableCollection<string> _locationPaths = new();

    public SettingsPageViewModel()
    {
        PageName = ApplicationPageNames.Settings;

        string appDir = AppContext.BaseDirectory;
        settingsFilePath = Path.Combine(appDir, "settings.json");
        cacheDirectory   = Path.Combine(appDir, "Cache");

        if (!Directory.Exists(cacheDirectory))
            Directory.CreateDirectory(cacheDirectory);

        LoadSettings();
    }

    // =====================================
    // ✅ LOAD + SAVE SETTINGS
    // =====================================
    private void LoadSettings()
    {
        if (!File.Exists(settingsFilePath))
        {
            // Defaults
            SkipFiles = false;
            AllowDuplicateEntries = false;
            LocationPaths = new ObservableCollection<string>();
            return;
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            var data = JsonSerializer.Deserialize<SettingsModel>(json);

            SkipFiles = data?.SkipFiles ?? false;
            AllowDuplicateEntries = data?.AllowDuplicateEntries ?? false;

            LocationPaths = new ObservableCollection<string>(
                data?.LocationPaths ?? new List<string>()
            );
        }
        catch
        {
            // Failed to load; fallback to defaults
            SkipFiles = false;
            AllowDuplicateEntries = false;
            LocationPaths = new ObservableCollection<string>();
        }
    }

    private void SaveSettings()
    {
        var data = new SettingsModel
        {
            SkipFiles = this.SkipFiles,
            AllowDuplicateEntries = this.AllowDuplicateEntries,
            LocationPaths = this.LocationPaths.ToList()
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsFilePath, json);
    }

    partial void OnSkipFilesChanged(bool value) => SaveSettings();
    partial void OnAllowDuplicateEntriesChanged(bool value) => SaveSettings();


    // =====================================
    // ✅ ADD FOLDER
    // =====================================
    [RelayCommand]
    private async Task AddFolderAsync()
    {
        
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);


        if (topLevel is null)
            return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            string path = folder[0].Path.LocalPath;
            if (!LocationPaths.Contains(path))
            {
                LocationPaths.Add(path);
                SaveSettings();
            }
        }
    }

    // =====================================
    // ✅ REMOVE SELECTED FOLDER
    // =====================================
    [RelayCommand]
    private void RemoveFolder(string? folderPath)
    {
        if (folderPath is null)
            return;

        if (LocationPaths.Contains(folderPath))
        {
            LocationPaths.Remove(folderPath);
            SaveSettings();
        }
    }

    // =====================================
    // ✅ CLEAR CACHE
    // =====================================
    [RelayCommand]
    private void ClearCache()
    {
        try
        {
            foreach (var file in Directory.GetFiles(cacheDirectory))
                File.Delete(file);
        }
        catch { }

        // Optionally: show confirmation dialog later
    }

    // =====================================
    // ✅ EXPORT CACHE
    // =====================================
    [RelayCommand]
    private async Task ExportCacheAsync()
    {
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);

        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Cache",
            DefaultExtension = "zip",
            SuggestedFileName = "CacheBackup.zip"
        });

        if (file == null) return;

        string zipPath = file.Path.LocalPath;

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        System.IO.Compression.ZipFile.CreateFromDirectory(cacheDirectory, zipPath);
    }


    // =====================================
    // ✅ IMPORT CACHE
    // =====================================
    [RelayCommand]
    private async Task ImportCacheAsync()
    {
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);

        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Cache",
            AllowMultiple = false
        });

        if (file.Count == 0) return;

        string zipPath = file[0].Path.LocalPath;

        // Clear current cache
        if (Directory.Exists(cacheDirectory))
            Directory.Delete(cacheDirectory, true);

        Directory.CreateDirectory(cacheDirectory);

        // 
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, cacheDirectory);
    }

}


// =====================================
//  SETTINGS MODEL 
// =====================================
public class SettingsModel
{
    public bool SkipFiles { get; set; }
    public bool AllowDuplicateEntries { get; set; }

    public List<string> LocationPaths { get; set; } = new();
}
