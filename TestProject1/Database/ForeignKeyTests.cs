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

}