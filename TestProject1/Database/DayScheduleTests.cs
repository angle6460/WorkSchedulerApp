using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    public class DayScheduleTests : DatabaseTestBase
    {
        [Test]
        public void CloneWeeklyWorkloadToSchedule_Creates7DaySchedules()
        {
            // Arrange
            var db = DatabaseHandler.Instance;

            // 1. Create a WeeklyWorkload template
            int weeklyWorkloadId = db.InsertWeeklyWorkloadTemplate("Default Template", "For testing DaySchedule creation");

            // 2. Add 7 DayWorkloads for each weekday
            string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            foreach (var day in days)
                db.InsertDayWorkload(day, weeklyWorkloadId);

            // 3. Define the week range (Nov 10–16, 2025 for example)
            DateTime weekStart = new(2025, 11, 10);
            DateTime weekEnd = weekStart.AddDays(6);

            // Act
            int scheduleId = db.CloneWeeklyWorkloadToSchedule(weeklyWorkloadId, weekStart, weekEnd);
            var daySchedules = db.GetDaySchedules(scheduleId);

            // Assert
            Assert.That(scheduleId, Is.GreaterThan(0), "WeeklySchedule should be created.");
            Assert.That(daySchedules.Count, Is.EqualTo(7), "There should be 7 DaySchedule entries for the week.");

            // Ensure all dates are consecutive and within week range
            var orderedDates = daySchedules.Select(ds => ds.date).OrderBy(d => d).ToList();
            Assert.That(orderedDates.First(), Is.EqualTo(weekStart), "First DaySchedule should match week start date.");
            Assert.That(orderedDates.Last(), Is.EqualTo(weekEnd), "Last DaySchedule should match week end date.");

            // Ensure all DaySchedules belong to the same WeeklySchedule
            Assert.That(daySchedules.All(ds => ds.weeklyScheduleId == scheduleId), "All DaySchedules should reference the same WeeklySchedule.");

            Console.WriteLine($"Created WeeklyScheduleID={scheduleId} with {daySchedules.Count} days.");
            foreach (var (id, schedId, date, workload) in daySchedules)
            {
                Console.WriteLine($" - DayScheduleID={id}, Date={date:yyyy-MM-dd}, LinkedWorkload={workload}");
            }
        }
    }
}