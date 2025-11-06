namespace WorkSchedulerApp.Models.WorkLoads
{
    public class BaseWorkLoad
    {
        public int WorkLoadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; 
        public int EstimatedHours { get; set; }
        public string Day { get; set; } = string.Empty;
    }
}