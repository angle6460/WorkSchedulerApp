using WorkSchedulerApp.TestProject1.Database;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Scheduling;

namespace WorkSchedulerApp.TestProject1.Scheduling
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ScheduleAutoAssignerTests : DatabaseTestBase
    {
        [Test]
        public async Task AssignEmployeesToWeeklyWorkloadInstance_AssignsCorrectEmployees()
        {
            var db = DatabaseHandler.Instance;

            // 1️⃣ Create employees with skills
            var empA = "EMP_" + Guid.NewGuid().ToString("N");
            var empB = "EMP_" + Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(empA, "Alice", "Cashier", 40, "Mon-Fri", "Full-Time");
            await db.InsertEmployeeAsync(empB, "Bob", "Stocker", 40, "Mon-Fri", "Full-Time");

            // 2️⃣ Create workloads
            var tplCashier = await db.InsertFixedWorkLoadTemplateAsync("Register", "Cashiering", 2);
            var tplStocker = await db.InsertPerItemWorkLoadTemplateAsync("Stock Shelves", "Restock", 5, 24);

            // Assign skills
            await db.AddTemplateSkillToEmployeeAsync(empA, tplCashier);
            await db.AddTemplateSkillToEmployeeAsync(empB, tplStocker);

            // 3️⃣ Build a weekly template and attach workloads to specific days
            var wkTplId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Week AutoAssign", "Testing auto assigner");
            var days = await db.GetDayWorkloadTemplatesForWeeklyTemplateAsync(wkTplId);
            var monTpl = days.First(d => d.day.Equals("Monday", StringComparison.OrdinalIgnoreCase)).id;
            var tueTpl = days.First(d => d.day.Equals("Tuesday", StringComparison.OrdinalIgnoreCase)).id;

            await db.AddWorkLoadTemplateToDayAsync(monTpl, tplCashier);
            await db.AddWorkLoadTemplateToDayAsync(tueTpl, tplStocker);

            // 4️⃣ Clone to instance (creates DayWorkloadInstances & WorkLoadInstances)
            var weekStart = new DateTime(2025, 1, 6);
            var weekEnd = weekStart.AddDays(6);
            var wkInstanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkTplId, weekStart, weekEnd);

            // 5️⃣ Run auto-assigner
            var results = await ScheduleAutoAssigner.AssignEmployeesToWeeklyWorkloadInstanceAsync(db, wkInstanceId);

            // 6️⃣ Assert results
            Assert.That(results.Count, Is.EqualTo(2), "Should assign one employee to each workload instance");

            var assignedIds = results.Select(r => r.employeeId).ToList();
            Assert.That(assignedIds, Does.Contain(empA), "Alice should be assigned to Register workload");
            Assert.That(assignedIds, Does.Contain(empB), "Bob should be assigned to Stock Shelves workload");
        }

        [Test]
        public async Task AssignEmployeesToWeeklyWorkloadInstance_HandlesNoQualifiedEmployees()
        {
            var db = DatabaseHandler.Instance;

            // Create workload with no qualified employees
            var tplUnassigned = await db.InsertFixedWorkLoadTemplateAsync("Deep Clean", "Nobody can do this", 3);

            var wkTplId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Week Empty", "No qualified employees");
            var days = await db.GetDayWorkloadTemplatesForWeeklyTemplateAsync(wkTplId);
            var monTpl = days.First(d => d.day.Equals("Monday", StringComparison.OrdinalIgnoreCase)).id;
            await db.AddWorkLoadTemplateToDayAsync(monTpl, tplUnassigned);

            var weekStart = new DateTime(2025, 1, 6);
            var wkInstanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkTplId, weekStart, weekStart.AddDays(6));

            // Run
            var results = await ScheduleAutoAssigner.AssignEmployeesToWeeklyWorkloadInstanceAsync(db, wkInstanceId);

            Assert.That(results.Count, Is.EqualTo(0), "No assignments should occur when no qualified employees exist.");
        }
    }
}
