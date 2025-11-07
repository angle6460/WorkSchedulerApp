using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Scheduling;

namespace WorkSchedulerApp.ViewModels;

public partial class WeeklySchedulePageViewModel : PageViewModel
{
    private readonly DatabaseHandler db;

    // Left pane: create schedule
    [ObservableProperty]
    private ObservableCollection<WeeklyTemplateSummary> templates = new();

    [ObservableProperty]
    private WeeklyTemplateSummary? selectedTemplate;

    [ObservableProperty]
    private DateTimeOffset? selectedStartDate = DateTimeOffset.Now;

    // Right pane: schedules list
    [ObservableProperty]
    private ObservableCollection<WeeklyScheduleSummary> schedules = new();

    [ObservableProperty]
    private WeeklyScheduleSummary? selectedSchedule;

    // Details panel: days → workloads for the selected schedule
    [ObservableProperty]
    private ObservableCollection<DayInstanceDetail> dayDetails = new();

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
        var summary = new WeeklyScheduleSummary
        {
            Id = instanceId,
            TemplateId = SelectedTemplate.Id,
            TemplateName = SelectedTemplate.Name,
            StartDate = start,
            EndDate = end
        };

        Schedules.Add(summary);
        SelectedSchedule = summary; // focus new schedule
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
        DayDetails.Clear();
    }

    // (kept for completeness; no longer used by UI)
    [RelayCommand]
    public void OpenSchedule()
    {
        // No-op; the UI shows details automatically
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

        // Reload details to reflect any changes in assignments or hours if needed
        await LoadScheduleDetailsAsync();
    }

    // When a schedule is selected, load its full details (days + workloads)
    partial void OnSelectedScheduleChanged(WeeklyScheduleSummary? value)
    {
        _ = LoadScheduleDetailsAsync();
    }

    public async Task LoadScheduleDetailsAsync()
    {
        DayDetails.Clear();

        if (SelectedSchedule == null)
            return;

        // 1) Get all day instances for this schedule
        var dayRows = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(SelectedSchedule.Id);

        foreach (var (dayInstanceId, dayName) in dayRows)
        {
            var dayDetail = new DayInstanceDetail
            {
                DayName = dayName
            };

            // 2) Get workload instances for the day
            var workloads = await db.GetWorkLoadInstancesForDayInstanceAsync(dayInstanceId);

            foreach (var (workLoadInstanceId, workLoadTemplateId) in workloads)
            {
                // 3) Resolve template to display details (name, desc, type, estimated hours)
                var tpl = await db.GetWorkLoadTemplateByIdAsync(workLoadTemplateId);
                if (tpl.HasValue)
                {
                    var (name, desc, hours, type) = tpl.Value;

                    dayDetail.Workloads.Add(new WorkLoadInstanceDetail
                    {
                        WorkLoadInstanceId = workLoadInstanceId,
                        Name = name,
                        Description = desc,
                        Type = type,
                        EstimatedHours = hours
                    });
                }
            }

            DayDetails.Add(dayDetail);
        }
    }
}

// ===== UI models =====
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

public class WorkLoadInstanceDetail
{
    public int WorkLoadInstanceId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public double EstimatedHours { get; set; }
}

public class DayInstanceDetail
{
    public string DayName { get; set; } = "";
    public ObservableCollection<WorkLoadInstanceDetail> Workloads { get; set; } = new();
}
