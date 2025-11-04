using System;
using System.Collections.Generic;

namespace WorkSchedulerApp.Models.WorkLoads
{
    public class PerItemWorkLoad : IWorkLoad
    {
        public string Name { get; set; } = "PerItem";
        public string Description { get; set; } = "";
        public int EstimatedHours { get; set; }
        public int WorkLoadId { get; set; }

        public int MinutesPerItem { get; set; }
        public int NumberOfItems { get; set; }

        public PerItemWorkLoad() { }

        public PerItemWorkLoad(int minutesPerItem, int numberOfItems, string description = "")
        {
            MinutesPerItem = minutesPerItem;
            NumberOfItems = numberOfItems;
            Description = description;
            EstimatedHours = (int)Math.Ceiling(CalculateHours());
        }

        public double CalculateHours()
        {
            return (MinutesPerItem * NumberOfItems) / 60.0;
        }

        public string GetDetails()
        {
            return $"{Name} Workload: {NumberOfItems} items × {MinutesPerItem} minutes = {CalculateHours():0.##} hours";
        }

        public void Load(string key, Dictionary<string, string> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Name = key;
            Description = data.TryGetValue("Description", out var desc) ? desc : "";
            MinutesPerItem = data.TryGetValue("MinutesPerItem", out var mpi) ? int.Parse(mpi) : 0;
            NumberOfItems = data.TryGetValue("NumberOfItems", out var noi) ? int.Parse(noi) : 0;
            EstimatedHours = data.TryGetValue("EstimatedHours", out var eh) ? int.Parse(eh) : (int)Math.Ceiling(CalculateHours());
        }

        public Tuple<string, Dictionary<string, string>> Save()
        {
            var data = new Dictionary<string, string>
            {
                { "Description", Description },
                { "MinutesPerItem", MinutesPerItem.ToString() },
                { "NumberOfItems", NumberOfItems.ToString() },
                { "EstimatedHours", EstimatedHours.ToString() }
            };

            return new Tuple<string, Dictionary<string, string>>(Name, data);
        }
    }
}
