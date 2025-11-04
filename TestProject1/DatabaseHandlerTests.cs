using NUnit.Framework;
using WorkScedulerApp.Database;
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace TestProject1;

[TestFixture]
public class DatabaseHandlerTests
{
    private string _testDbPath;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        // Create a temp DB file
        _testDbPath = Path.Combine(Path.GetTempPath(), $"TestDB_{Guid.NewGuid()}.db");
        Console.WriteLine($"[TEST DB PATH] {_testDbPath}");
        var connectionString = $"Data Source={_testDbPath};";

        // Configure the singleton connection (auto-creates schema)
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.ConnectionString = connectionString;

        Assert.That(File.Exists(_testDbPath), Is.True, "Database file should exist after schema creation.");
    }

    // ------------------------------------------------------------
    // Insert Tests
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
    // View Tests
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
    // Cleanup
    // ------------------------------------------------------------

    [OneTimeTearDown]
    public void Cleanup()
    {
        DatabaseHandler.Instance.Close();

        Console.WriteLine($"Inspect DB at: {_testDbPath}");
        Console.WriteLine("Press ENTER to delete...");
        Console.ReadLine(); // wait to inspect db before deleting

        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

}
