using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Scheduling;


namespace WorkSchedulerApp.ViewModels;

public partial class WeeklySchedulePageViewModel : PageViewModel
{
    private readonly DatabaseHandler db;

    [ObservableProperty]
    private ObservableCollection<WeeklyTemplateSummary> templates = new();

    [ObservableProperty]
    private WeeklyTemplateSummary? selectedTemplate;

    [ObservableProperty]
    private DateTimeOffset? selectedStartDate = DateTimeOffset.Now;

    [ObservableProperty]
    private ObservableCollection<WeeklyScheduleSummary> schedules = new();

    [ObservableProperty]
    private WeeklyScheduleSummary? selectedSchedule;

    public WeeklySchedulePageViewModel()
    {
        PageName = ApplicationPageNames.WeeklySchedules;
        db = DatabaseHandler.Instance;

        _ = LoadTemplatesAsync();
        _ = LoadSchedulesAsync();
    }

    // Load weekly template list for schedule creation
    [RelayCommand]
    public async Task LoadTemplatesAsync()
    {
        Templates.Clear();
        var rows = await db.GetAllWeeklyWorkloadTemplatesAsync();
        foreach (var (id, name, desc) in rows)
        {
            Templates.Add(new WeeklyTemplateSummary
            {
                Id = id,
                Name = name,
                Description = desc
            });
        }
    }

    // Load existing weekly schedule instances
    [RelayCommand]
    public async Task LoadSchedulesAsync()
    {
        Schedules.Clear();

        // Make sure templates are loaded
        await LoadTemplatesAsync();

        // Fetch instances
        var rows = await db.GetAllWeeklyWorkloadInstancesAsync();

        foreach (var (id, tmplId, start, end) in rows)
        {
            // find template
            var tmpl = Templates.FirstOrDefault(t => t.Id == tmplId);

            Schedules.Add(new WeeklyScheduleSummary
            {
                Id = id,
                TemplateId = tmplId,
                TemplateName = tmpl?.Name ?? "(Unknown Template)",
                StartDate = DateTime.Parse(start),
                EndDate = DateTime.Parse(end)
            });
        }
    }


    // Create new weekly schedule from template
    [RelayCommand]
    public async Task CreateScheduleAsync()
    {
        if (SelectedTemplate == null || SelectedStartDate == null)
            return;

        var start = SelectedStartDate.Value.Date;  // Convert to DateTime
        var end = start.AddDays(6);

        int instanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(
            SelectedTemplate.Id, start, end
        );

        // Add to list
        Schedules.Add(new WeeklyScheduleSummary
        {
            Id = instanceId,
            TemplateId = SelectedTemplate.Id,
            TemplateName = SelectedTemplate.Name,
            StartDate = start,
            EndDate = end
        });
    }

    // Delete schedule
    [RelayCommand]
    public async Task DeleteScheduleAsync()
    {
        if (SelectedSchedule == null)
            return;

        await db.DeleteWeeklyWorkloadInstanceAsync(SelectedSchedule.Id);
        Schedules.Remove(SelectedSchedule);
        SelectedSchedule = null;
    }

    // Open schedule details
    [RelayCommand]
    public void OpenSchedule()
    {
        if (SelectedSchedule == null)
            return;

        // TODO: implement navigation service
        // Example:
        // Navigation.Navigate(new WeeklySchedulePageViewModel(SelectedSchedule.Id));
    }

    // Run auto assigner
    [RelayCommand]
    public async Task AutoAssignAsync()
    {
        if (SelectedSchedule == null)
            return;

        await ScheduleAutoAssigner.AssignEmployeesToWeeklyWorkloadInstanceAsync(
            db, SelectedSchedule.Id
        );

        // Optionally notify UI
    }
}

public class WeeklyScheduleSummary
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public string Range => $"{StartDate:MMM dd} - {EndDate:MMM dd}";
    public string TemplateLabel => $"Template: {TemplateName}";
}


