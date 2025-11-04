using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkSchedulerApp.Models.WorkLoads
{
    public class WorkGroup : IWorkLoad
    {
        public int WorkLoadId { get; set; }

        public string Name { get; set; } = "WorkGroup";
        public string Description { get; set; } = "";
        public int EstimatedHours { get; set; }
        

        public List<IWorkLoad> SubWorkLoads { get; } = new();

        public WorkGroup() { }

        public WorkGroup(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

        public void AddWorkLoad(IWorkLoad workLoad)
        {
            if (workLoad != null)
                SubWorkLoads.Add(workLoad);
        }

        public void RemoveWorkLoad(IWorkLoad workLoad)
        {
            if (workLoad != null)
                SubWorkLoads.Remove(workLoad);
        }

        public double CalculateHours()
        {
            return SubWorkLoads.Sum(w => w.CalculateHours());
        }

        public string GetDetails()
        {
            var details = string.Join("\n", SubWorkLoads.Select(w => "  - " + w.GetDetails()));
            return $"{Name} Group ({SubWorkLoads.Count} workloads)\n{details}\nTotal Hours: {CalculateHours():0.##}";
        }

        public void Load(string key, Dictionary<string, string> data)
        {
            Name = key;
            Description = data.TryGetValue("Description", out var desc) ? desc : "";
        }

        public Tuple<string, Dictionary<string, string>> Save()
        {
            var data = new Dictionary<string, string>
            {
                { "Description", Description },
                { "EstimatedHours", ((int)Math.Ceiling(CalculateHours())).ToString() },
                { "SubWorkLoadCount", SubWorkLoads.Count.ToString() }
            };

            return new Tuple<string, Dictionary<string, string>>(Name, data);
        }
    }
}