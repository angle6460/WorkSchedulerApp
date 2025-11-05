namespace WorkSchedulerApp.Models.Scheduling;

public class Shift
{
    public int ShiftId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Each shift may have multiple breaks
    public List<ShiftBreak> Breaks { get; set; } = new();
}
