using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class WeeklyScheduleTests : DatabaseTestBase
{
    private DatabaseHandler db = null!;

    [SetUp]
    public void Setup() => db = DatabaseHandler.Instance;

    [Test]
    public void InsertWeeklySchedule_CreatesRecord()
    {
        int id = db.InsertWeeklySchedule(
            new DateTime(2025, 11, 10),
            new DateTime(2025, 11, 16),
            null
        );

        var schedules = db.GetAllWeeklySchedules();
        Assert.That(schedules.Count, Is.GreaterThan(0));
        Assert.That(schedules.Any(s => s.id == id));
    }

    [Test]
    public void AssignEmployeeToSchedule_CreatesLink()
    {
        string employeeId = Guid.NewGuid().ToString();
        db.InsertEmployee(employeeId, "Alice", "Cashier", 30, "Mon-Fri 9–5", "Part-time");

        int scheduleId = db.InsertWeeklySchedule(DateTime.Today, DateTime.Today.AddDays(7));

        db.AssignEmployeeToSchedule(scheduleId, employeeId);

        var employees = db.GetEmployeesForSchedule(scheduleId);
        Assert.That(employees.Contains("Alice"));
    }

    [Test]
    public void AssignEmployeeToDayWorkload_CreatesMapping()
    {
        // Setup data
        string employeeId = Guid.NewGuid().ToString();
        db.InsertEmployee(employeeId, "Bob", "Stocker", 20, "Mon–Thu", "Casual");

        int templateId = db.InsertWeeklyWorkloadTemplate("Standard", "Template for tests");
        int dayWorkloadId = db.InsertDayWorkload("Monday", templateId);

        int scheduleId = db.InsertWeeklySchedule(DateTime.Today, DateTime.Today.AddDays(7), templateId);

        // Test
        db.AssignEmployeeToDayWorkload(employeeId, scheduleId, dayWorkloadId);

        var assignments = db.GetEmployeeDayAssignments(scheduleId);
        Assert.That(assignments.Any(a => a.employeeId == employeeId && a.dayWorkloadId == dayWorkloadId));
    }
}