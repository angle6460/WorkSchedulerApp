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
}