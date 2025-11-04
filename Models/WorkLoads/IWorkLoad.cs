using System;
using System.Collections.Generic;

namespace WorkSchedulerApp.Models.WorkLoads;

public interface IWorkLoad
{
    string Name { get; set; }

    double CalculateHours();
    string GetDetails();
    void Load(string key, Dictionary<string, string> value);
    Tuple<string, Dictionary<string, string>> Save();
}