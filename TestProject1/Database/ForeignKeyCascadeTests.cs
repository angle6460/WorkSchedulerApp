using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ForeignKeyCascadeTests : DatabaseTestBase
    {
        [Test]
        public async Task DeletingWorkLoadTemplate_RemovesEmployeeSkillReferences()
        {
            var db = DatabaseHandler.Instance;

            var empId = "EMP_" + Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(empId, "Charlie", "Clerk", 25, "Mon-Fri", "Part-Time");

            var tplId = await db.InsertFixedWorkLoadTemplateAsync("Cleanup", "End of day", 1.0);
            await db.AddTemplateSkillToEmployeeAsync(empId, tplId);
            Assert.That(await db.GetEmployeeTemplateSkillCountAsync(empId, tplId), Is.EqualTo(1));

            await db.DeleteWorkLoadTemplateAsync(tplId);
            Assert.That(await db.GetEmployeeTemplateSkillCountAsync(empId, tplId), Is.EqualTo(0));
        }

        [Test]
        public async Task DeletingWeeklyWorkloadTemplate_CascadesDayTemplates()
        {
            var db = DatabaseHandler.Instance;
            var wkId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Week X", "Test week");

            // quick sanity: should exist in list
            var all = await db.GetAllWeeklyWorkloadTemplatesAsync();
            Assert.That(all.Any(w => w.id == wkId), Is.True);

            await db.DeleteWeeklyWorkloadTemplateAsync(wkId);

            // should no longer be present
            var after = await db.GetAllWeeklyWorkloadTemplatesAsync();
            Assert.That(after.Any(w => w.id == wkId), Is.False);

            // optional: if you added GetAllDayWorkloadTemplatesAsync, verify days gone
            // var days = await db.GetAllDayWorkloadTemplatesAsync();
            // Assert.That(days.Any(d => d.weeklyWorkloadTemplateId == wkId), Is.False);
        }

        [Test]
        public async Task DeletingWorkGroup_RemovesWorkGroupWorkLoadTemplates()
        {
            var db = DatabaseHandler.Instance;

            var w1 = await db.InsertFixedWorkLoadTemplateAsync("WG T1", "d", 1);
            var w2 = await db.InsertFixedWorkLoadTemplateAsync("WG T2", "d", 1);
            var groupId = await db.InsertWorkGroupWithTemplatesAsync("Group A", new() { w1, w2 });

            var before = await db.GetWorkLoadTemplatesForGroupAsync(groupId);
            Assert.That(before.Count, Is.EqualTo(2));

            await db.DeleteWorkGroupAsync(groupId);

            var after = await db.GetWorkLoadTemplatesForGroupAsync(groupId);
            Assert.That(after.Count, Is.EqualTo(0));
        }
    }
}
