using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class TemplateLinkingTests : DatabaseTestBase
    {
        [Test]
        public async Task AddWorkLoadTemplateToDay_CreatesDayMapping()
        {
            var db = DatabaseHandler.Instance;

            var wkId = await db.InsertWeeklyWorkloadTemplateAsync("Week Links", "");
            var dayId = await db.InsertDayWorkloadTemplateAsync("Thursday", wkId);

            var tpl = await db.InsertFixedWorkLoadTemplateAsync("Price Check", "Audits", 1);
            await db.AddWorkLoadTemplateToDayAsync(dayId, tpl);

            // Clone and verify day instance includes workload
            var inst = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkId, new(2025, 1, 6), new(2025, 1, 12));
            var days = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(inst);
            var thu = days.First(d => d.day.Equals("Thursday", System.StringComparison.OrdinalIgnoreCase));
            var loads = await db.GetWorkLoadInstancesForDayInstanceAsync(thu.dayWorkloadInstanceId);
            Assert.That(loads.Any(l => l.workLoadTemplateId == tpl), Is.True);
        }

        [Test]
        public async Task EmployeeTemplateSkills_AddAndRemove_Works()
        {
            var db = DatabaseHandler.Instance;

            var empId = "EMP_" + System.Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(empId, "Eve", "General", 20, "Mon-Fri", "PT");

            var tpl = await db.InsertFixedWorkLoadTemplateAsync("Wrapping", "Gift wrap", 1);
            await db.AddTemplateSkillToEmployeeAsync(empId, tpl);

            var all = await db.GetAllEmployeeTemplateSkillsAsync();
            Assert.That(all.Any(x => x.employeeId == empId && x.workLoadTemplateId == tpl), Is.True);

            await db.RemoveTemplateSkillFromEmployeeAsync(empId, tpl);
            var after = await db.GetAllEmployeeTemplateSkillsAsync();
            Assert.That(after.Any(x => x.employeeId == empId && x.workLoadTemplateId == tpl), Is.False);
        }
    }
}
