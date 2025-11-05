using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class EmployeeTests : DatabaseTestBase
{
    [Test]
    public void InsertEmployee_CreatesRecord()
    {
        var db = DatabaseHandler.Instance;
        string id = Guid.NewGuid().ToString();

        db.InsertEmployee(id, "Alex Rivera", "Cashier", 30, "Mon–Fri 9:00–15:00", "Part-time");
        var all = db.GetAllEmployees();

        Assert.That(all.Exists(e => e.id == id));
    }

    [Test]
    public void GetAllEmployees_ReturnsInsertedEmployees()
    {
        var db = DatabaseHandler.Instance;
        string id = Guid.NewGuid().ToString();

        db.InsertEmployee(id, "Jamie Lee", "Supervisor", 38, "Mon–Fri 8:00–16:00", "Full-time");
        var all = db.GetAllEmployees();

        Assert.That(all.Count, Is.GreaterThan(0));
        Assert.That(all.Exists(e => e.name == "Jamie Lee"));
    }

    [Test]
    public void GetEmployeeById_ReturnsCorrectDetails()
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
}