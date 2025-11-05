namespace WorkSchedulerApp.Models.Scheduling;

public class EmployeeDailySchedule
{
    public int EmployeeDailyScheduleId { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public int WeeklyScheduleId { get; set; }
    public int DayWorkloadId { get; set; }
}
