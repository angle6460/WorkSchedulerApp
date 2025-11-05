namespace WorkSchedulerApp.Models.Scheduling;

public class DayWorkload
{
    public int DayWorkloadId { get; set; }
    public string Day { get; set; } = string.Empty; // e.g. "Monday"
    public int WeeklyWorkloadId { get; set; }
    public List<int> WorkLoadIds { get; set; } = new();
}
