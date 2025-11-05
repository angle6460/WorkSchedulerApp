namespace WorkSchedulerApp.Models.Scheduling;

public class WeeklyWorkload
{
    public int WeeklyWorkloadId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<DayWorkload> DayWorkloads { get; set; } = new();
}
