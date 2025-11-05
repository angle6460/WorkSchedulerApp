namespace WorkSchedulerApp.Models.Scheduling;


public class WeeklySchedule
{
    public int WeeklyScheduleId { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int? WeeklyWorkloadId { get; set; }

    public List<EmployeeWeeklySchedule> EmployeeSchedules { get; set; } = new();
}
