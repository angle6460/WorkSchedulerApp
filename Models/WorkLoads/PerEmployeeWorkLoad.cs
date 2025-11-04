using System;
using System.Collections.Generic;

namespace WorkSchedulerApp.Models.WorkLoads
{
    public class PerEmployeeWorkLoad : IWorkLoad
    {
        public int WorkLoadId { get; set; }

        public string Name { get; set; } = "PerEmployee";
        public string Description { get; set; } = "";
        public int EstimatedHours { get; set; }
        

        public int MinutesPerEmployee { get; set; }
        public int NumberOfEmployees { get; set; }

        public PerEmployeeWorkLoad() { }

        public PerEmployeeWorkLoad(int minutesPerEmployee, int numberOfEmployees, string description = "")
        {
            MinutesPerEmployee = minutesPerEmployee;
            NumberOfEmployees = numberOfEmployees;
            Description = description;
            EstimatedHours = (int)Math.Ceiling(CalculateHours());
        }

        public double CalculateHours()
        {
            return (MinutesPerEmployee * NumberOfEmployees) / 60.0;
        }

        public string GetDetails()
        {
            return $"{Name} Workload: {NumberOfEmployees} employees × {MinutesPerEmployee} minutes = {CalculateHours():0.##} hours";
        }

        public void Load(string key, Dictionary<string, string> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Name = key;
            Description = data.TryGetValue("Description", out var desc) ? desc : "";
            MinutesPerEmployee = data.TryGetValue("MinutesPerEmployee", out var mpe) ? int.Parse(mpe) : 0;
            NumberOfEmployees = data.TryGetValue("NumberOfEmployees", out var noe) ? int.Parse(noe) : 0;
            EstimatedHours = data.TryGetValue("EstimatedHours", out var eh) ? int.Parse(eh) : (int)Math.Ceiling(CalculateHours());
        }

        public Tuple<string, Dictionary<string, string>> Save()
        {
            var data = new Dictionary<string, string>
            {
                { "Description", Description },
                { "MinutesPerEmployee", MinutesPerEmployee.ToString() },
                { "NumberOfEmployees", NumberOfEmployees.ToString() },
                { "EstimatedHours", EstimatedHours.ToString() }
            };

            return new Tuple<string, Dictionary<string, string>>(Name, data);
        }
    }
}
