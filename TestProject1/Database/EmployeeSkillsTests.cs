using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class EmployeeSkillsTests : DatabaseTestBase
{
    [Test]
    public void AddSkillToEmployee_CreatesMapping()
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
    public void GetSkillsForEmployee_ReturnsMappedSkills()
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
    public void GetEmployeesForSkill_ReturnsMappedEmployees()
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
    }

    [Test]
    public void RemoveSkillFromEmployee_DeletesMapping()
    {
        var db = DatabaseHandler.Instance;
        db.InsertFixedWorkLoad("Unload Truck", "Unload shipments", 2);
        int workLoadId = db.GetWorkLoadIdByName("Unload Truck");

        string empId = Guid.NewGuid().ToString();
        db.InsertEmployee(empId, "Jamie Park", "Logistics", 32, "Mon–Fri", "Full-time");

        db.AddSkillToEmployee(empId, workLoadId);
        Assert.That(db.GetEmployeeSkillCount(empId, workLoadId), Is.EqualTo(1));

        db.RemoveSkillFromEmployee(empId, workLoadId);
        Assert.That(db.GetEmployeeSkillCount(empId, workLoadId), Is.EqualTo(0));
    }
}
