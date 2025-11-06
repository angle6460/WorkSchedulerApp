using System.Collections.Generic;

namespace WorkSchedulerApp.Models
{
    public class Employee
    {
        public string EmployeeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int RequestedHours { get; set; }
        public string Availability { get; set; } = string.Empty;
        public string ContractedHours { get; set; } = string.Empty;

        // NEW: Skills used by the scheduler
        public List<string> Skills { get; set; } = new();

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

        // NEW: Convert ContractedHours string → numeric maximum
        public int GetMaxContractedHours()
        {
            if (int.TryParse(ContractedHours, out int val))
                return val;

            // fallback rules
            if (ContractedHours.Contains("Full", StringComparison.OrdinalIgnoreCase))
                return 38;
            if (ContractedHours.Contains("Part", StringComparison.OrdinalIgnoreCase))
                return 20;
            return 10; // casual or unknown
        }
        public bool IsAvailableOn(string day)
        {
            if (string.IsNullOrWhiteSpace(Availability))
                return false;

            // Normalize day input (e.g., "Monday" → "Mon")
            var shortDay = day[..3].ToLowerInvariant();

            // Convert common formats into a list of available days
            var availability = Availability.ToLowerInvariant().Replace("–", "-");

            // Handle ranges like "mon-fri"
            if (availability.Contains('-'))
            {
                var parts = availability.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var days = new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };
                    var startIdx = Array.IndexOf(days, parts[0].Trim());
                    var endIdx = Array.IndexOf(days, parts[1].Trim());
                    if (startIdx >= 0 && endIdx >= 0)
                    {
                        var range = startIdx <= endIdx
                            ? days[startIdx..(endIdx + 1)]
                            : days[startIdx..].Concat(days[..(endIdx + 1)]).ToArray();

                        return range.Contains(shortDay);
                    }
                }
            }

            // Handle comma-separated list like "mon,tue,thu"
            var allowedDays = availability.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Substring(0, 3))
                .ToList();

            return allowedDays.Contains(shortDay);
        }

    }
}