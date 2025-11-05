namespace WorkSchedulerApp.Models
{
    public class Employee
    {
        public string EmployeeId { get; set; } = Guid.NewGuid().ToString(); // TEXT primary key
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int RequestedHours { get; set; }
        public string Availability { get; set; } = string.Empty;
        public string ContractedHours { get; set; } = string.Empty; // matches TEXT column

        public Employee() { }

        public Employee(string name, string role, int requestedHours, string contractedHours, string availability)
        {
            EmployeeId = Guid.NewGuid().ToString();
            Name = name;
            Role = role;
            RequestedHours = requestedHours;
            ContractedHours = contractedHours;
            Availability = availability;
        }

        public string GetInfo()
        {
            return $"{Name} ({Role}) — {RequestedHours}h requested, {ContractedHours} contracted";
        }
    }
}