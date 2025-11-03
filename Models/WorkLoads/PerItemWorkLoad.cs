using System;
using System.Collections.Generic;

namespace WorkScedulerApp.Models.WorkLoads;

public class PerItemWorkLoad : IWorkLoad
{
    private string _name = "PerItem";

    public PerItemWorkLoad()
    {
        
    }
    public double CalculateHours()
    {
        throw new System.NotImplementedException();
    }
    public string Name {
        get
        {
            return _name;
        }
        set
        {
            _name = value;
        }
    }

    public string GetDetails()
    {
        throw new System.NotImplementedException();
    }

    public void Load(string key, Dictionary<string, string> value)
    {
        throw new System.NotImplementedException();
    }

    public Tuple<string, Dictionary<string, string>> Save()
    {
        throw new System.NotImplementedException();
    }
}