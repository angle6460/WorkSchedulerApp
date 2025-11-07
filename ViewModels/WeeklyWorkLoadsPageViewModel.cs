using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Database;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;


namespace WorkSchedulerApp.ViewModels;

public partial class WeeklyWorkLoadsPageViewModel : PageViewModel
{
    private readonly DatabaseHandler db;

    [ObservableProperty]
    private ObservableCollection<WeeklyTemplateSummary> weeklyTemplates = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemplateSelected))]
    private WeeklyTemplateSummary? selectedTemplate;
    
    public bool TemplateSelected => selectedTemplate != null;
    [ObservableProperty]
    private ObservableCollection<DayTemplateVM> days = new();

    [ObservableProperty]
    private DayTemplateVM? selectedDay;

    [ObservableProperty]
    private ObservableCollection<WorkLoadTemplateVM> workloads = new();

    [ObservableProperty]
    private WorkLoadTemplateVM? selectedWorkload;

    public WeeklyWorkLoadsPageViewModel()
    {
        PageName = ApplicationPageNames.WeeklyTemplates;
        db = DatabaseHandler.Instance;

        _ = LoadWeeklyTemplatesAsync();
    }

    // ------------------------------------------------------------
    // Load Weekly Templates
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task LoadWeeklyTemplatesAsync()
    {
        WeeklyTemplates.Clear();

        var rows = await db.GetAllWeeklyWorkloadTemplatesAsync();

        foreach (var (id, name, description) in rows)
        {
            WeeklyTemplates.Add(new WeeklyTemplateSummary
            {
                Id = id,
                Name = name,
                Description = description
            });
        }
    }

    [RelayCommand]
    public async Task AddWeeklyTemplateAsync()
    {
        int newId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync(
            "New Weekly Template",
            "Description"
        );

        WeeklyTemplates.Add(new WeeklyTemplateSummary
        {
            Id = newId,
            Name = "New Weekly Template",
            Description = "Description"
        });
    }

    [RelayCommand]
    public async Task DeleteWeeklyTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        await db.DeleteWeeklyWorkloadTemplateAsync(SelectedTemplate.Id);
        WeeklyTemplates.Remove(SelectedTemplate);
        SelectedTemplate = null;

        Days.Clear();
        Workloads.Clear();
    }

    partial void OnSelectedTemplateChanged(WeeklyTemplateSummary? value)
    {
        _ = LoadDaysForTemplateAsync();
    }

    // ------------------------------------------------------------
    // Load Days for Selected Template
    // ------------------------------------------------------------
    private async Task LoadDaysForTemplateAsync()
    {
        Days.Clear();
        Workloads.Clear();
        SelectedDay = null;

        if (SelectedTemplate == null)
            return;

        var dayRows = await db.GetDayWorkloadTemplatesForWeeklyTemplateAsync(SelectedTemplate.Id);

        foreach (var day in dayRows)
        {
            Days.Add(new DayTemplateVM
            {
                Id = day.id,
                Name = day.day
            });
        }
    }

    partial void OnSelectedDayChanged(DayTemplateVM? value)
    {
        _ = LoadWorkloadsForDayAsync();
    }

    // ------------------------------------------------------------
    // Load Workloads for Selected Day
    // ------------------------------------------------------------
    private async Task LoadWorkloadsForDayAsync()
    {
        Workloads.Clear();

        if (SelectedDay == null)
            return;

        var rows = await db.GetWorkLoadTemplatesForDayAsync(SelectedDay.Id);
        foreach (var (id, name, desc, type, hours) in rows)
        {
            Workloads.Add(new WorkLoadTemplateVM
            {
                Id = id,
                Name = name,
                Description = desc,
                Type = type,
                EstimatedHours = hours
            });
        }
    }

    // ------------------------------------------------------------
    // Add Workload to Day
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task AddWorkloadToDayAsync()
    {
        if (SelectedDay == null)
            return;

        // TODO: open a dialog to pick workload
        // For now, select first workload template as placeholder
        var wltList = await db.GetAllWorkLoadTemplatesAsync();
        if (wltList.Count == 0)
            return;

        int wltId = wltList.First().id;

        await db.AddWorkLoadTemplateToDayAsync(SelectedDay.Id, wltId);

        await LoadWorkloadsForDayAsync();
    }

    // ------------------------------------------------------------
    // Remove Workload from Day
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task RemoveSelectedWorkloadAsync()
    {
        if (SelectedDay == null || SelectedWorkload == null)
            return;

        await db.RemoveWorkLoadTemplateFromDayAsync(SelectedDay.Id, SelectedWorkload.Id);

        await LoadWorkloadsForDayAsync();
    }
    [RelayCommand]
    public async Task RenameSelectedTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        // TODO: Replace with a real dialog later
        string? newName = await ShowTextInputDialogAsync(
            "Rename Template",
            "Enter new name:",
            SelectedTemplate.Name
        );

        if (string.IsNullOrWhiteSpace(newName))
            return;

        await db.UpdateWeeklyWorkloadTemplateNameAsync(SelectedTemplate.Id, newName);

        // Update locally so UI refreshes
        SelectedTemplate.Name = newName;

        // Force UI update (because WeeklyTemplateSummary is not ObservableObject)
        WeeklyTemplates = new ObservableCollection<WeeklyTemplateSummary>(WeeklyTemplates);
    }
    [RelayCommand]
    public async Task EditDescriptionAsync()
    {
        if (SelectedTemplate == null)
            return;

        string? newDesc = await ShowTextInputDialogAsync(
            "Edit Description",
            "Enter new description:",
            SelectedTemplate.Description
        );

        if (string.IsNullOrWhiteSpace(newDesc))
            return;

        await db.UpdateWeeklyWorkloadTemplateDescriptionAsync(SelectedTemplate.Id, newDesc);

        SelectedTemplate.Description = newDesc;

        WeeklyTemplates = new ObservableCollection<WeeklyTemplateSummary>(WeeklyTemplates);
    }
    private async Task<string?> ShowTextInputDialogAsync(string title, string prompt, string current)
    {
        var dialog = new WorkSchedulerApp.Views.TextInputDialog();
        dialog.Init(title, prompt, current);

        var mainWindow = (Application.Current?.ApplicationLifetime as
            IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        return await dialog.ShowDialogAsync(mainWindow!);
    }




}

// ------------------------------------------------------------
// Supporting ViewModels
// ------------------------------------------------------------
public class WeeklyTemplateSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class DayTemplateVM
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class WorkLoadTemplateVM
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public double EstimatedHours { get; set; }
}
