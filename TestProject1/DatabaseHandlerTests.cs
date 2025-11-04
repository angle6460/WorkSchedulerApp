using Microsoft.Data.Sqlite;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1;

[TestFixture]
public class DatabaseHandlerTests
{
    private string _testDbPath = string.Empty;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        // Base directory of test binaries
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Create a "TestDB" folder next to the binaries
        var testDbDir = Path.Combine(baseDir, "TestDB");
        Directory.CreateDirectory(testDbDir);

        // Create a unique DB file for this test session
        _testDbPath = Path.Combine(testDbDir, $"TestDB_{Guid.NewGuid()}.db");
        Console.WriteLine($"[TEST DB PATH] {_testDbPath}");

        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);

        var connectionString = $"Data Source={_testDbPath};";

        // Configure singleton and trigger schema creation
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.ConnectionString = connectionString;

        Assert.That(File.Exists(_testDbPath), Is.True, "Database file should exist after schema creation.");
    }

    // ------------------------------------------------------------
    // Core Insert Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_InsertPerEmployeeWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerEmployeeWorkLoad("Meeting", "10 minute huddle", 10, 3);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WorkLoad WHERE Name='Meeting';";
        int count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Test_InsertPerItemWorkLoad_CreatesSubtypeRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerItemWorkLoad("Packaging", "Wrap items", 5, 10);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PerItemWorkLoad;";
        int count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public void Test_InsertFixedWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Cleaning", "Night cleanup", 2);

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM FixedWorkLoad;";
        int count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public void Test_InsertWorkGroup_MapsWorkLoads()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Restock", "Restock shelves", 1);

        int workLoadId;
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name='Restock';";
            workLoadId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        db.InsertWorkGroup("Morning Crew", new List<int> { workLoadId });

        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM WorkGroupWorkLoad;";
            int count = Convert.ToInt32(cmd.ExecuteScalar());

            Assert.That(count, Is.GreaterThan(0));
        }
    }

    // ------------------------------------------------------------
    // View / Retrieval Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_GetAllWorkLoads_ReturnsInserted()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Refill", "Refill supplies", 1);

        var all = db.GetAllWorkLoads();

        Assert.That(all.Count, Is.GreaterThan(0));
        Assert.That(all.Exists(w => w.name == "Refill"));
    }

    [Test]
    public void Test_GetWorkLoadById_ReturnsCorrectDetails()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Maintenance", "Clean equipment", 2);

        int id;
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name='Maintenance';";
            id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        var wl = db.GetWorkLoadById(id);
        Assert.That(wl.HasValue, Is.True);
        Assert.That(wl?.name, Is.EqualTo("Maintenance"));
        Assert.That(wl?.type, Is.EqualTo("Fixed"));
    }

    [Test]
    public void Test_GetAllWorkGroups_ReturnsCreatedGroup()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Stock", "Stock shelves", 1);

        int workLoadId;
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name='Stock';";
            workLoadId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        db.InsertWorkGroup("Evening Shift", new List<int> { workLoadId });
        var groups = db.GetAllWorkGroups();

        Assert.That(groups.Count, Is.GreaterThan(0));
        Assert.That(groups.Exists(g => g.name == "Evening Shift"));
    }

    [Test]
    public void Test_GetWorkLoadsForGroup_ReturnsMappedWorkLoads()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Unload Truck", "Unload shipments", 3);

        int workLoadId;
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name='Unload Truck';";
            workLoadId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        db.InsertWorkGroup("Logistics", new List<int> { workLoadId });

        int groupId;
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT WorkGroupID FROM WorkGroup WHERE Name='Logistics';";
            groupId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        var mapped = db.GetWorkLoadsForGroup(groupId);
        Assert.That(mapped.Count, Is.GreaterThan(0));
        Assert.That(mapped.Exists(w => w.workLoadId == workLoadId));
    }

    // ------------------------------------------------------------
    // Foreign-Key Enforcement Test (Option 2)
    // ------------------------------------------------------------

    [Test]
    public void Test_ForeignKeys_AreEnabled()
    {
        var db = DatabaseHandler.Instance;

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        int result = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(result, Is.EqualTo(1), "Foreign key enforcement should be ON.");
    }

    // ------------------------------------------------------------
    // Cleanup
    // ------------------------------------------------------------

    [OneTimeTearDown]
    public void Cleanup()
    {
        DatabaseHandler.Instance.Close();
    }
}
