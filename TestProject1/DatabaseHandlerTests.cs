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
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var testDbDir = Path.Combine(baseDir, "TestDB");
        Directory.CreateDirectory(testDbDir);

        _testDbPath = Path.Combine(testDbDir, $"TestDB_{Guid.NewGuid()}.db");
        Console.WriteLine($"[TEST DB PATH] {_testDbPath}");

        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);

        var connectionString = $"Data Source={_testDbPath};";
        DatabaseHandler.Instance.Initialize(connectionString);

        Assert.That(File.Exists(_testDbPath), Is.True, "Database file should exist after schema creation.");
    }

    // ------------------------------------------------------------
    // WorkLoad Insert & Retrieval Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_InsertPerEmployeeWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerEmployeeWorkLoad("Meeting", "10 minute huddle", 10, 3);
        int count = db.GetWorkLoadCountByName("Meeting");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Test_InsertPerItemWorkLoad_CreatesSubtypeRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertPerItemWorkLoad("Packaging", "Wrap items", 5, 10);
        int count = db.GetWorkLoadCountByName("Packaging");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Test_InsertFixedWorkLoad_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Cleaning", "Night cleanup", 2);
        int count = db.GetWorkLoadCountByName("Cleaning");
        Assert.That(count, Is.EqualTo(1));
    }

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
        int id = db.GetWorkLoadIdByName("Maintenance");

        var wl = db.GetWorkLoadById(id);
        Assert.That(wl.HasValue, Is.True);
        Assert.That(wl?.name, Is.EqualTo("Maintenance"));
        Assert.That(wl?.type, Is.EqualTo("Fixed"));
    }

    // ------------------------------------------------------------
    // WorkGroup Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_InsertWorkGroup_MapsWorkLoads()
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
    public void Test_GetWorkLoadsForGroup_ReturnsMappedWorkLoads()
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

    // ------------------------------------------------------------
    // Employee Table Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_InsertEmployee_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        string id = Guid.NewGuid().ToString();

        db.InsertEmployee(id, "Alex Rivera", "Cashier", 30, "Mon–Fri 9:00–15:00", "Part-time");
        var all = db.GetAllEmployees();

        Assert.That(all.Exists(e => e.id == id));
    }

    [Test]
    public void Test_GetAllEmployees_ReturnsInsertedEmployees()
    {
        var db = DatabaseHandler.Instance;
        string id = Guid.NewGuid().ToString();

        db.InsertEmployee(id, "Jamie Lee", "Supervisor", 38, "Mon–Fri 8:00–16:00", "Full-time");
        var all = db.GetAllEmployees();

        Assert.That(all.Count, Is.GreaterThan(0));
        Assert.That(all.Exists(e => e.name == "Jamie Lee"));
    }

    [Test]
    public void Test_GetEmployeeById_ReturnsCorrectDetails()
    {
        var db = DatabaseHandler.Instance;
        string id = Guid.NewGuid().ToString();

        db.InsertEmployee(id, "Taylor Chen", "Manager", 40, "Mon–Fri 9:00–17:00", "Full-time");

        var employee = db.GetEmployeeById(id);
        Assert.That(employee.HasValue);
        Assert.That(employee?.name, Is.EqualTo("Taylor Chen"));
        Assert.That(employee?.role, Is.EqualTo("Manager"));
        Assert.That(employee?.requestedHours, Is.EqualTo(40));
    }

    // ------------------------------------------------------------
    // Employee Skills Tests
    // ------------------------------------------------------------

    [Test]
    public void Test_AddSkillToEmployee_CreatesMapping()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Operate Register", "Handles cash transactions", 1);
        int workLoadId = db.GetWorkLoadIdByName("Operate Register");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Riley Park", "Cashier", 25, "Mon–Fri", "Part-time");

        db.AddSkillToEmployee(empId, workLoadId);
        int count = db.GetEmployeeSkillCount(empId, workLoadId);

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Test_GetSkillsForEmployee_ReturnsMappedSkills()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Stock Shelves", "Organize inventory", 2);
        int workLoadId = db.GetWorkLoadIdByName("Stock Shelves");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Ava Nguyen", "Store Assistant", 20, "Sat–Sun", "Casual");
        db.AddSkillToEmployee(empId, workLoadId);

        var skills = db.GetSkillsForEmployee(empId);
        Assert.That(skills.Count, Is.EqualTo(1));
        Assert.That(skills[0].workLoadId, Is.EqualTo(workLoadId));
    }

    [Test]
    public void Test_GetEmployeesForSkill_ReturnsMappedEmployees()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Customer Service", "Assist customers", 1);
        int workLoadId = db.GetWorkLoadIdByName("Customer Service");

        string emp1 = Guid.NewGuid().ToString();
        string emp2 = Guid.NewGuid().ToString();

        db.InsertEmployee(emp1, "Taylor Nguyen", "Assistant", 30, "Mon–Fri", "Full-time");
        db.InsertEmployee(emp2, "Riley Chen", "Supervisor", 35, "Tue–Sat", "Full-time");

        db.AddSkillToEmployee(emp1, workLoadId);
        db.AddSkillToEmployee(emp2, workLoadId);

        var employees = db.GetEmployeesForSkill(workLoadId);
        Assert.That(employees.Count, Is.EqualTo(2));
        Assert.That(employees.Exists(e => e.employeeName == "Taylor Nguyen"));
        Assert.That(employees.Exists(e => e.employeeName == "Riley Chen"));
    }

    [Test]
    public void Test_RemoveSkillFromEmployee_DeletesMapping()
    {
        var db = DatabaseHandler.Instance;

        db.InsertFixedWorkLoad("Unload Truck", "Unload shipments", 2);
        int workLoadId = db.GetWorkLoadIdByName("Unload Truck");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Jamie Park", "Logistics", 32, "Mon–Fri", "Full-time");

        db.AddSkillToEmployee(empId, workLoadId);
        int before = db.GetEmployeeSkillCount(empId, workLoadId);
        Assert.That(before, Is.EqualTo(1));

        db.RemoveSkillFromEmployee(empId, workLoadId);
        int after = db.GetEmployeeSkillCount(empId, workLoadId);
        Assert.That(after, Is.EqualTo(0));
    }

    // ------------------------------------------------------------
    // Foreign-Key Enforcement Test
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
