namespace WorkSchedulerApp.Models.Scheduling;

public class ShiftBreak
{
    public int BreakId { get; set; }
    public int ShiftId { get; set; }
    public DateTime BreakTime { get; set; }
}
