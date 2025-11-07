using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Scheduling;

namespace WorkSchedulerApp.ViewModels;

public partial class WeeklySchedulePageViewModel : PageViewModel
{
    private readonly DatabaseHandler db;

    // Left pane: create schedule
    [ObservableProperty] private ObservableCollection<WeeklyTemplateSummary> templates = new();
    [ObservableProperty] private WeeklyTemplateSummary? selectedTemplate;
    [ObservableProperty] private DateTimeOffset? selectedStartDate = DateTimeOffset.Now;

    // Right pane: schedules list
    [ObservableProperty] private ObservableCollection<WeeklyScheduleSummary> schedules = new();
    [ObservableProperty] private WeeklyScheduleSummary? selectedSchedule;

    // Details panel
    [ObservableProperty] private ObservableCollection<DayInstanceDetail> dayDetails = new();

    // ✅ NEW EXPORT COMMAND
    public IAsyncRelayCommand ExportAssignedEmployeesCommand { get; }

    public WeeklySchedulePageViewModel()
    {
        PageName = ApplicationPageNames.WeeklySchedules;
        db = DatabaseHandler.Instance;

        ExportAssignedEmployeesCommand = new AsyncRelayCommand(ExportAssignedEmployeesAsync);

        _ = LoadTemplatesAsync();
        _ = LoadSchedulesAsync();
    }

    // Load weekly template list
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

    // Load existing weekly schedules
    [RelayCommand]
    public async Task LoadSchedulesAsync()
    {
        Schedules.Clear();
        await LoadTemplatesAsync();

        var rows = await db.GetAllWeeklyWorkloadInstancesAsync();

        foreach (var (id, tmplId, start, end) in rows)
        {
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

    // Create new schedule
    [RelayCommand]
    public async Task CreateScheduleAsync()
    {
        if (SelectedTemplate == null || SelectedStartDate == null)
            return;

        var start = SelectedStartDate.Value.Date;
        var end = start.AddDays(6);

        int instanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(
            SelectedTemplate.Id, start, end
        );

        var summary = new WeeklyScheduleSummary
        {
            Id = instanceId,
            TemplateId = SelectedTemplate.Id,
            TemplateName = SelectedTemplate.Name,
            StartDate = start,
            EndDate = end
        };

        Schedules.Add(summary);
        SelectedSchedule = summary;
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

    // Auto Assign
    [RelayCommand]
    public async Task AutoAssignAsync()
    {
        if (SelectedSchedule == null)
            return;

        await ScheduleAutoAssigner.AssignEmployeesToWeeklyWorkloadInstanceAsync(
            db, SelectedSchedule.Id
        );

        await LoadScheduleDetailsAsync();
    }

    // When selection changes, load details
    partial void OnSelectedScheduleChanged(WeeklyScheduleSummary? value)
    {
        _ = LoadScheduleDetailsAsync();
    }

    // Load full details for a selected schedule
    public async Task LoadScheduleDetailsAsync()
    {
        DayDetails.Clear();

        if (SelectedSchedule == null)
            return;

        var dayRows = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(SelectedSchedule.Id);

        foreach (var (dayInstanceId, dayName) in dayRows)
        {
            var dayDetail = new DayInstanceDetail { DayName = dayName };
            var workloads = await db.GetWorkLoadInstancesForDayInstanceAsync(dayInstanceId);

            foreach (var (workLoadInstanceId, workLoadTemplateId) in workloads)
            {
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

    // ✅ NEW EXPORT FUNCTION — NO SQL USED
    private async Task ExportAssignedEmployeesAsync()
    {
        if (SelectedSchedule == null)
            return;

        // ✅ Build a path next to Database.db
        var dbDirectory = Path.Combine(AppContext.BaseDirectory, "Database");

        if (!Directory.Exists(dbDirectory))
            Directory.CreateDirectory(dbDirectory);

        string filePath = Path.Combine(
            dbDirectory,
            $"AssignedEmployees_Schedule_{SelectedSchedule.Id}.csv"
        );

        var sb = new StringBuilder();
        sb.AppendLine("Employee,Task,Hours,Day");

        // 1. Get the day instances for the selected schedule
        var days = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(SelectedSchedule.Id);

        foreach (var (dayInstanceId, dayName) in days)
        {
            // 2. Get workload instances for this day
            var workloads = await db.GetWorkLoadInstancesForDayInstanceAsync(dayInstanceId);

            foreach (var (wliId, tplId) in workloads)
            {
                // 3. Template info
                var tpl = await db.GetWorkLoadTemplateByIdAsync(tplId);
                if (tpl == null) continue;

                var (taskName, desc, hours, type) = tpl.Value;

                // 4. Assigned employees
                var employees = await db.GetEmployeesAssignedToWorkLoadInstanceAsync(wliId);

                foreach (var (empId, empName) in employees)
                {
                    sb.AppendLine($"{empName},{taskName},{hours},{dayName}");
                }
            }
        }

        // ✅ Save file next to Database.db
        File.WriteAllText(filePath, sb.ToString());

        Console.WriteLine($"✅ Exported assigned employees to: {filePath}");
    }

}


// ===== UI Models =====

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
