using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class WeeklyScheduleCloneTests : DatabaseTestBase
{
    private DatabaseHandler db = null!;

    [SetUp]
    public void Setup() => db = DatabaseHandler.Instance;

    [Test]
    public void CloneWeeklyWorkloadToSchedule_CopiesDayWorkloadsAndMappings()
    {
        // 1. Create template
        int templateId = db.InsertWeeklyWorkloadTemplate("Retail Template", "Standard week");
        int mondayId = db.InsertDayWorkload("Monday", templateId);
        int tuesdayId = db.InsertDayWorkload("Tuesday", templateId);

        // Add workloads
        db.InsertFixedWorkLoad("Clean Storefront", "Morning task", 2);
        var workloads = db.GetAllWorkLoads();
        int wlId = workloads.First().id;

        // Map workload to Monday
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO DayWorkloadWorkLoad (DayWorkloadID, WorkLoadID) VALUES ($day, $wl);";
            cmd.Parameters.AddWithValue("$day", mondayId);
            cmd.Parameters.AddWithValue("$wl", wlId);
            cmd.ExecuteNonQuery();
        }

        // 2. Clone
        int scheduleId = db.CloneWeeklyWorkloadToSchedule(templateId, DateTime.Today, DateTime.Today.AddDays(7));

        // 3. Verify new schedule exists
        var schedules = db.GetAllWeeklySchedules();
        Assert.That(schedules.Any(s => s.id == scheduleId));

        // 4. Verify new DayWorkloads exist
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM DayWorkload;";
            int count = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.That(count, Is.GreaterThan(2), "Should include cloned DayWorkloads.");
        }
    }
}
