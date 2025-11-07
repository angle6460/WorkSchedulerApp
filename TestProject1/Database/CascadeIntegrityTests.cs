using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class CascadeIntegrityTests : DatabaseTestBase
    {
        [Test]
        public async Task EndToEnd_CascadesHoldUnderComplexGraph()
        {
            var db = DatabaseHandler.Instance;

            // Create employee and skills
            var emp = "EMP_" + System.Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(emp, "Frank", "Multi", 38, "Mon-Sun", "FT");

            var tpl1 = await db.InsertFixedWorkLoadTemplateAsync("Open", "Open store", 1);
            var tpl2 = await db.InsertPerEmployeeWorkLoadTemplateAsync("Team Brief", "Daily standup", 10, 6);
            await db.AddTemplateSkillToEmployeeAsync(emp, tpl1);
            await db.AddTemplateSkillToEmployeeAsync(emp, tpl2);

            // Weekly + Days + attach templates to days
            var wkId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Full Week", "all");
            var monId = await db.InsertDayWorkloadTemplateAsync("Monday", wkId);
            var friId = await db.InsertDayWorkloadTemplateAsync("Friday", wkId);
            await db.AddWorkLoadTemplateToDayAsync(monId, tpl1);
            await db.AddWorkLoadTemplateToDayAsync(friId, tpl2);

            // Clone to instance and sanity-check something exists
            var inst = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkId, new(2025, 1, 6), new(2025, 1, 12));
            var days = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(inst);
            Assert.That(days.Count, Is.GreaterThan(0));

            // Delete WorkLoadTemplate #1 and verify its links/instances will vanish in future clones
            await db.DeleteWorkLoadTemplateAsync(tpl1);

            // Now delete the weekly template — should cascade all day templates & mappings
            await db.DeleteWeeklyWorkloadTemplateAsync(wkId);

            // Verify weekly template gone
            var weeks = await db.GetAllWeeklyWorkloadTemplatesAsync();
            Assert.That(weeks.Any(w => w.id == wkId), Is.False);

            // Finally delete employee — should cascade EmployeeSkill rows
            await db.DeleteEmployeeAsync(emp);
            Assert.That(await db.GetEmployeeTemplateSkillCountAsync(emp, tpl2), Is.EqualTo(0));
        }
    }
}
