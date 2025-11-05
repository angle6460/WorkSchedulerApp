using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class WorkLoadTests : DatabaseTestBase
{
    [Test]
    public void InsertPerEmployeeWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerEmployeeWorkLoad("Meeting", "10 minute huddle", 10, 3);
        int count = db.GetWorkLoadCountByName("Meeting");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void InsertPerItemWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerItemWorkLoad("Packaging", "Wrap items", 5, 10);
        int count = db.GetWorkLoadCountByName("Packaging");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void InsertFixedWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Cleaning", "Night cleanup", 2);
        int count = db.GetWorkLoadCountByName("Cleaning");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void GetAllWorkLoads_ReturnsInserted()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Refill", "Refill supplies", 1);
        var all = db.GetAllWorkLoads();

        Assert.That(all.Count, Is.GreaterThan(0));
        Assert.That(all.Exists(w => w.name == "Refill"));
    }

    [Test]
    public void GetWorkLoadById_ReturnsCorrectDetails()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Maintenance", "Clean equipment", 2);
        int id = db.GetWorkLoadIdByName("Maintenance");

        var wl = db.GetWorkLoadById(id);
        Assert.That(wl.HasValue);
        Assert.That(wl?.name, Is.EqualTo("Maintenance"));
        Assert.That(wl?.type, Is.EqualTo("Fixed"));
    }
}