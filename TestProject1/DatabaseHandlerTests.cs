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
        _testDbPath = Path.Combine(Path.GetTempPath(), $"TestDB_{Guid.NewGuid()}.db");
        var connectionString = $"Data Source={_testDbPath};";

        // Set connection string (triggers EnsureSchema automatically)
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.ConnectionString = connectionString;

        Assert.That(File.Exists(_testDbPath), Is.True, "Database file should exist after schema creation.");
    }

    [Test]
    public void Test_InsertPerEmployeeWorkLoad_CreatesRecord()
    {
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.InsertPerEmployeeWorkLoad("Meeting", "10 minute huddle", 10, 3);

        using var connection = new SqliteConnection(dbHandler.ConnectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WorkLoad WHERE Name = 'Meeting';";
        var count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.EqualTo(1), "Expected one inserted PerEmployeeWorkLoad record.");
    }

    [Test]
    public void Test_InsertPerItemWorkLoad_CreatesSubtypeRecord()
    {
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.InsertPerItemWorkLoad("Packaging", "Wrap items", 5, 10);

        using var connection = new SqliteConnection(dbHandler.ConnectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PerItemWorkLoad;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.GreaterThan(0), "PerItemWorkLoad should have entries.");
    }

    [Test]
    public void Test_InsertFixedWorkLoad_CreatesRecord()
    {
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.InsertFixedWorkLoad("Cleaning", "Night cleanup", 2);

        using var connection = new SqliteConnection(dbHandler.ConnectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM FixedWorkLoad;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public void Test_InsertWorkGroup_MapsWorkLoads()
    {
        var dbHandler = DatabaseHandler.Instance;
        dbHandler.InsertFixedWorkLoad("Restock", "Restock shelves", 1);

        // Get workload ID
        int workLoadId;
        using (var connection = new SqliteConnection(dbHandler.ConnectionString))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name='Restock';";
            workLoadId = Convert.ToInt32(cmd.ExecuteScalar());
        }

        dbHandler.InsertWorkGroup("Morning Crew", new List<int> { workLoadId });

        using (var connection = new SqliteConnection(dbHandler.ConnectionString))
        {
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM WorkGroupWorkLoad;";
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            Assert.That(count, Is.GreaterThan(0), "WorkGroupWorkLoad should map at least one record.");
        }
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        DatabaseHandler.Instance.Close();

        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

}
