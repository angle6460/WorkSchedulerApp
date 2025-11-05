using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class WorkGroupTests : DatabaseTestBase
{
    [Test]
    public void InsertWorkGroup_MapsWorkLoads()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Restock", "Restock shelves", 1);
        int workLoadId = db.GetWorkLoadIdByName("Restock");

        db.InsertWorkGroup("Morning Crew", new List<int> { workLoadId });
        var groups = db.GetAllWorkGroups();

        Assert.That(groups.Count, Is.GreaterThan(0));
        Assert.That(groups.Exists(g => g.name == "Morning Crew"));
    }

    [Test]
    public void GetWorkLoadsForGroup_ReturnsMappedWorkLoads()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Unload Truck", "Unload shipments", 3);
        int workLoadId = db.GetWorkLoadIdByName("Unload Truck");

        db.InsertWorkGroup("Logistics", new List<int> { workLoadId });
        var groups = db.GetAllWorkGroups();
        int groupId = groups.First(g => g.name == "Logistics").id;

        var mapped = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mapped.Count, Is.GreaterThan(0));
        Assert.That(mapped.Exists(w => w.workLoadId == workLoadId));
    }
    
    [Test]
    public void UpdateWorkGroup_ChangesNameSuccessfully()
    {
        var db = DatabaseHandler.Instance;

        // Create workload and group
        db.InsertFixedWorkLoad("Setup Shelves", "Organize items", 2);
        int workLoadId = db.GetWorkLoadIdByName("Setup Shelves");
        db.InsertWorkGroup("Morning Setup", new List<int> { workLoadId });

        var groups = db.GetAllWorkGroups();
        int groupId = groups.First(g => g.name == "Morning Setup").id;

        // Update name
        db.UpdateWorkGroup(groupId, "Opening Crew");

        var updatedGroups = db.GetAllWorkGroups();
        Assert.That(updatedGroups.Exists(g => g.name == "Opening Crew"), Is.True);
    }

    [Test]
    public void DeleteWorkGroup_RemovesGroup()
    {
        var db = DatabaseHandler.Instance;

        // Create workload and group
        db.InsertFixedWorkLoad("Unload Pallets", "Stock goods", 2);
        int workLoadId = db.GetWorkLoadIdByName("Unload Pallets");
        db.InsertWorkGroup("Warehouse Team", new List<int> { workLoadId });

        var groups = db.GetAllWorkGroups();
        int groupId = groups.First(g => g.name == "Warehouse Team").id;

        // Delete
        db.DeleteWorkGroup(groupId);

        var afterDelete = db.GetAllWorkGroups();
        Assert.That(afterDelete.Exists(g => g.id == groupId), Is.False, "Group should be removed after deletion.");
    }

}