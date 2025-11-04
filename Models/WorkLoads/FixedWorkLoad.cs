using System;
using System.Collections.Generic;

namespace WorkSchedulerApp.Models.WorkLoads
{
    public class FixedWorkLoad : IWorkLoad
    {
        

        public string Name { get; set; } = "Fixed";
        public string Description { get; set; } = "";
        public int EstimatedHours { get; set; }
        public int WorkLoadId { get; set; }

        public int FixedHours { get; set; }

        public FixedWorkLoad() { }

        public FixedWorkLoad(int fixedHours, string description = "")
        {
            FixedHours = fixedHours;
            Description = description;
            EstimatedHours = fixedHours;
        }

        public double CalculateHours()
        {
            return FixedHours;
        }

        public string GetDetails()
        {
            return $"{Name} Workload: {FixedHours} fixed hours total";
        }

        public void Load(string key, Dictionary<string, string> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Name = key;
            Description = data.TryGetValue("Description", out var desc) ? desc : "";
            FixedHours = data.TryGetValue("FixedHours", out var fh) ? int.Parse(fh) : 0;
            EstimatedHours = data.TryGetValue("EstimatedHours", out var eh) ? int.Parse(eh) : FixedHours;
        }

        public Tuple<string, Dictionary<string, string>> Save()
        {
            var data = new Dictionary<string, string>
            {
                { "Description", Description },
                { "FixedHours", FixedHours.ToString() },
                { "EstimatedHours", EstimatedHours.ToString() }
            };

            return new Tuple<string, Dictionary<string, string>>(Name, data);
        }
    }
}