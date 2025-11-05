using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    public class DaySchedulePartialTemplateTests : DatabaseTestBase
    {
        [Test]
        public void CloneWeeklyWorkload_PartialTemplate_FillsAll7Days()
        {
            // Arrange
            var db = DatabaseHandler.Instance;

            // 1. Create a WeeklyWorkload template with only Mon–Fri
            int weeklyWorkloadId = db.InsertWeeklyWorkloadTemplate("Partial Template", "Only weekdays defined");
            string[] definedDays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };

            foreach (var day in definedDays)
                db.InsertDayWorkload(day, weeklyWorkloadId);

            // 2. Define week range (example)
            DateTime weekStart = new(2025, 11, 10); // Monday
            DateTime weekEnd = weekStart.AddDays(6);

            // Act
            int scheduleId = db.CloneWeeklyWorkloadToSchedule(weeklyWorkloadId, weekStart, weekEnd);
            var daySchedules = db.GetDaySchedules(scheduleId);

            // Assert basic structure
            Assert.That(scheduleId, Is.GreaterThan(0), "WeeklySchedule should be created.");
            Assert.That(daySchedules.Count, Is.EqualTo(7), "Should always create 7 DaySchedules (Mon–Sun).");

            // Check that weekday links exist, weekends are null
            var linked = daySchedules.Where(d => d.dayWorkloadId.HasValue).ToList();
            var unlinked = daySchedules.Where(d => !d.dayWorkloadId.HasValue).ToList();

            Assert.That(linked.Count, Is.EqualTo(5), "Only 5 days should have workload links (Mon–Fri).");
            Assert.That(unlinked.Count, Is.EqualTo(2), "2 weekend days should have no workloads.");

            // Ensure linked days correspond to Mon–Fri
            var linkedDates = linked.Select(d => d.date.DayOfWeek.ToString()).ToList();
            foreach (var expectedDay in definedDays)
                Assert.That(linkedDates, Does.Contain(expectedDay));

            // Ensure Saturday and Sunday are unlinked
            var weekendNames = unlinked.Select(d => d.date.DayOfWeek.ToString()).ToList();
            Assert.That(weekendNames, Does.Contain("Saturday"));
            Assert.That(weekendNames, Does.Contain("Sunday"));

            Console.WriteLine($"Created WeeklyScheduleID={scheduleId} with 7 days ({linked.Count} linked).");
            foreach (var (id, schedId, date, workload) in daySchedules)
            {
                Console.WriteLine($" - {date:dddd} → WorkloadID={(workload?.ToString() ?? "None")}");
            }
        }
    }
}
