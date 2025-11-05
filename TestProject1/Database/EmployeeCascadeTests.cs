using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    public class EmployeeCascadeTests : DatabaseTestBase
    {
        [Test]
        public void DeletingEmployee_RemovesSkillsAndSchedules()
        {
            // Arrange
            var db = DatabaseHandler.Instance;

            // 1 Create a workload template and a weekly schedule
            int workloadId = db.InsertFixedWorkLoad("Stocking", "Restock items", 2);
            int weeklyWorkloadId = db.InsertWeeklyWorkloadTemplate("Cascade Employee Template", "Test template");

            int mondayId = db.InsertDayWorkload("Monday", weeklyWorkloadId);
            int tuesdayId = db.InsertDayWorkload("Tuesday", weeklyWorkloadId);

            DateTime weekStart = new(2025, 11, 10);
            DateTime weekEnd = weekStart.AddDays(6);
            int scheduleId = db.CloneWeeklyWorkloadToSchedule(weeklyWorkloadId, weekStart, weekEnd);

            // 2 Create an employee and link them everywhere
            string empId = Guid.NewGuid().ToString();
            db.InsertEmployee(empId, "Cascade User", "Worker", 20, "Mon–Fri", "Part-time");
            db.AddSkillToEmployee(empId, workloadId);
            db.AssignEmployeeToSchedule(scheduleId, empId);
            db.AssignEmployeeToDayWorkload(empId, scheduleId, mondayId);
            db.AssignEmployeeToDayWorkload(empId, scheduleId, tuesdayId);

            // Sanity checks before deletion
            Assert.That(db.GetSkillsForEmployee(empId).Count, Is.EqualTo(1), "Employee should have 1 skill.");
            Assert.That(db.GetEmployeesForSchedule(scheduleId).Contains("Cascade User"), Is.True, "Employee should be assigned to schedule.");
            Assert.That(db.GetEmployeeDayAssignments(scheduleId).Any(a => a.employeeId == empId), Is.True, "Employee should have daily assignments.");

            // 3 Delete the employee
            db.DeleteEmployee(empId);

            // Verify cascading removed all related data
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(db.ConnectionString))
            {
                conn.Open();

                int skillCount, weeklyAssignCount, dailyAssignCount;

                // EmployeeSkills
                var cmd1 = conn.CreateCommand();
                cmd1.CommandText = "SELECT COUNT(*) FROM EmployeeSkills WHERE EmployeeID = $id;";
                cmd1.Parameters.AddWithValue("$id", empId);
                skillCount = Convert.ToInt32(cmd1.ExecuteScalar());

                // EmployeeWeeklySchedule
                var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM EmployeeWeeklySchedule WHERE EmployeeID = $id;";
                cmd2.Parameters.AddWithValue("$id", empId);
                weeklyAssignCount = Convert.ToInt32(cmd2.ExecuteScalar());

                // EmployeeDailySchedule
                var cmd3 = conn.CreateCommand();
                cmd3.CommandText = "SELECT COUNT(*) FROM EmployeeDailySchedule WHERE EmployeeID = $id;";
                cmd3.Parameters.AddWithValue("$id", empId);
                dailyAssignCount = Convert.ToInt32(cmd3.ExecuteScalar());

                // Assertions
                Assert.That(skillCount, Is.EqualTo(0), "EmployeeSkills should cascade delete.");
                Assert.That(weeklyAssignCount, Is.EqualTo(0), "EmployeeWeeklySchedule should cascade delete.");
                Assert.That(dailyAssignCount, Is.EqualTo(0), "EmployeeDailySchedule should cascade delete.");
            }

            Console.WriteLine($"Cascade delete for EmployeeID={empId} verified successfully.");
        }
    }
}
