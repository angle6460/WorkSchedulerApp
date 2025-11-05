using Microsoft.Data.Sqlite;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class ForeignKeyTests : DatabaseTestBase
{
    [Test]
    public void ForeignKeys_AreEnabled()
    {
        var db = DatabaseHandler.Instance;

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        int result = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(result, Is.EqualTo(1), "Foreign key enforcement should be ON.");
    }
    
    [Test]
    public void DeletingEmployee_RemovesEmployeeSkills()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Operate Register", "Handles transactions", 1);
        int workLoadId = db.GetWorkLoadIdByName("Operate Register");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Chris Lee", "Cashier", 25, "Mon–Fri", "Part-time");
        db.AddSkillToEmployee(empId, workLoadId);

        Assert.That(db.GetEmployeeSkillCount(empId, workLoadId), Is.EqualTo(1));

        db.DeleteEmployee(empId); 

        int remaining = db.GetEmployeeSkillCount(empId, workLoadId);
        Assert.That(remaining, Is.EqualTo(0));
    }

    [Test]
    public void DeletingWorkLoad_RemovesEmployeeSkills()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Stock Shelves", "Organize inventory", 2);
        int workLoadId = db.GetWorkLoadIdByName("Stock Shelves");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Riley Tran", "Store Assistant", 20, "Mon–Fri", "Casual");
        db.AddSkillToEmployee(empId, workLoadId);

        Assert.That(db.GetEmployeeSkillCount(empId, workLoadId), Is.EqualTo(1));

        db.DeleteWorkLoad(workLoadId); 

        int remaining = db.GetEmployeeSkillCount(empId, workLoadId);
        Assert.That(remaining, Is.EqualTo(0));
    }
    [Test]
    public void DeletingWorkGroup_RemovesWorkGroupWorkLoadMappings()
    {
        var db = DatabaseHandler.Instance;

        // Create a workload
        db.InsertFixedWorkLoad("Shelf Stocking", "Refill stock", 2);
        int workLoadId = db.GetWorkLoadIdByName("Shelf Stocking");

        // Create group and map workload
        db.InsertWorkGroup("Morning Team", new List<int> { workLoadId });
        var groups = db.GetAllWorkGroups();
        int groupId = groups.First(g => g.name == "Morning Team").id;

        // Verify mapping exists
        var mappedBefore = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mappedBefore.Count, Is.GreaterThan(0), "Mapping should exist before deletion.");

        // Delete group
        db.DeleteWorkGroup(groupId);

        // Verify cascade removed the mapping
        var mappedAfter = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mappedAfter.Count, Is.EqualTo(0), "Mappings should be deleted when the WorkGroup is deleted.");
    }
    
    [Test]
    public void DeletingWorkLoad_RemovesWorkGroupWorkLoadMappings()
    {
        var db = DatabaseHandler.Instance;

        // Create a workload and a work group
        db.InsertFixedWorkLoad("Unload Pallets", "Unload shipment pallets", 3);
        int workLoadId = db.GetWorkLoadIdByName("Unload Pallets");

        db.InsertWorkGroup("Warehouse Crew", new List<int> { workLoadId });
        var groups = db.GetAllWorkGroups();
        int groupId = groups.First(g => g.name == "Warehouse Crew").id;

        // Confirm mapping exists
        var mappedBefore = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mappedBefore.Count, Is.GreaterThan(0), "Mapping should exist before deleting WorkLoad.");

        // Delete the workload
        db.DeleteWorkLoad(workLoadId);

        // Confirm mapping is gone
        var mappedAfter = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mappedAfter.Count, Is.EqualTo(0), "WorkGroupWorkLoad mappings should be removed when WorkLoad is deleted.");
    }


}