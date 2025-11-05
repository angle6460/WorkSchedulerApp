using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    public class WeeklyScheduleCascadeTests : DatabaseTestBase
    {
        [Test]
        public void DeletingWeeklySchedule_RemovesLinkedDaySchedulesAndEmployeeDailySchedules()
        {
            // Arrange
            var db = DatabaseHandler.Instance;

            // 1 Create a WeeklyWorkload template and day definitions
            int templateId = db.InsertWeeklyWorkloadTemplate("Cascade Test Template", "For cascade testing");
            string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
            foreach (var day in days)
                db.InsertDayWorkload(day, templateId);

            // 2 Clone it into a WeeklySchedule
            DateTime weekStart = new(2025, 11, 10);
            DateTime weekEnd = weekStart.AddDays(6);
            int scheduleId = db.CloneWeeklyWorkloadToSchedule(templateId, weekStart, weekEnd);

            // 3 Add an employee and assign them to a few days
            string employeeId = Guid.NewGuid().ToString();
            db.InsertEmployee(employeeId, "Cascade Tester", "Staff", 20, "Mon–Fri", "Part-time");

            var daySchedules = db.GetDaySchedules(scheduleId);
            var monday = daySchedules.First().id;
            var tuesday = daySchedules.Skip(1).First().id;

            // Directly use EmployeeDailySchedule (simulate a working schedule)
            db.AssignEmployeeToSchedule(scheduleId, employeeId);
            db.AssignEmployeeToDayWorkload(employeeId, scheduleId, monday);
            db.AssignEmployeeToDayWorkload(employeeId, scheduleId, tuesday);

            // Sanity check
            var preDeleteDays = db.GetDaySchedules(scheduleId);
            Assert.That(preDeleteDays.Count, Is.EqualTo(7), "7 DaySchedules should exist before deletion.");
            var preDeleteAssignments = db.GetEmployeeDayAssignments(scheduleId);
            Assert.That(preDeleteAssignments.Count, Is.EqualTo(2), "EmployeeDailySchedule rows should exist before deletion.");

            // 4 Delete the WeeklySchedule
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM WeeklySchedule WHERE WeeklyScheduleID = $id;";
                cmd.Parameters.AddWithValue("$id", scheduleId);
                cmd.ExecuteNonQuery();
            }

            // 5 Verify cascading delete cleaned up related tables
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
            {
                conn.Open();

                int dayCount, dailyAssignCount;
                var cmd1 = conn.CreateCommand();
                cmd1.CommandText = "SELECT COUNT(*) FROM DaySchedule WHERE WeeklyScheduleID = $id;";
                cmd1.Parameters.AddWithValue("$id", scheduleId);
                dayCount = Convert.ToInt32(cmd1.ExecuteScalar());

                var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM EmployeeDailySchedule WHERE WeeklyScheduleID = $id;";
                cmd2.Parameters.AddWithValue("$id", scheduleId);
                dailyAssignCount = Convert.ToInt32(cmd2.ExecuteScalar());

                Assert.That(dayCount, Is.EqualTo(0), "All DaySchedule rows should be deleted with WeeklySchedule.");
                Assert.That(dailyAssignCount, Is.EqualTo(0), "All EmployeeDailySchedule rows should be deleted with WeeklySchedule.");
            }

            Console.WriteLine($"Cascade delete confirmed: WeeklyScheduleID={scheduleId} cleanup succeeded.");
        }
    }
}
