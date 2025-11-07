using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WorkLoadInstanceTests : DatabaseTestBase
    {
        [Test]
        public async Task CloneWeeklyWorkloadTemplateToInstance_CreatesWorkLoadInstancesCorrectly()
        {
            var db = DatabaseHandler.Instance;

            var tplA = await db.InsertFixedWorkLoadTemplateAsync("Open Store", "Prep", 1);
            var tplB = await db.InsertPerItemWorkLoadTemplateAsync("Pick Orders", "Online", 5, 12);

            var wkId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Week Sched", "demo");

            // Get existing Monday and Tuesday templates created automatically
            var days = await db.GetDayWorkloadTemplatesForWeeklyTemplateAsync(wkId);
            var monTplId = days.First(d => d.day.Equals("Monday", StringComparison.OrdinalIgnoreCase)).id;
            var tueTplId = days.First(d => d.day.Equals("Tuesday", StringComparison.OrdinalIgnoreCase)).id;

            await db.AddWorkLoadTemplateToDayAsync(monTplId, tplA);
            await db.AddWorkLoadTemplateToDayAsync(tueTplId, tplB);

            var start = new DateTime(2025, 1, 6);
            var end   = start.AddDays(6);
            var instanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkId, start, end);

            var dayInstances = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(instanceId);
            var monInstance = dayInstances.First(d => d.day.Equals("Monday", StringComparison.OrdinalIgnoreCase));
            var tueInstance = dayInstances.First(d => d.day.Equals("Tuesday", StringComparison.OrdinalIgnoreCase));

            var monLoads = await db.GetWorkLoadInstancesForDayInstanceAsync(monInstance.dayWorkloadInstanceId);
            var tueLoads = await db.GetWorkLoadInstancesForDayInstanceAsync(tueInstance.dayWorkloadInstanceId);

            Assert.That(monLoads.Any(x => x.workLoadTemplateId == tplA), Is.True, "Monday should include tplA");
            Assert.That(tueLoads.Any(x => x.workLoadTemplateId == tplB), Is.True, "Tuesday should include tplB");
        }


        [Test]
        public async Task AssignEmployeeToWorkLoadInstance_Works()
        {
            var db = DatabaseHandler.Instance;

            var empId = "EMP_" + Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(empId, "Dana", "Associate", 20, "Mon-Fri", "PT");

            var tpl = await db.InsertFixedWorkLoadTemplateAsync("Close Store", "Shutdown", 1);

            // Create a weekly template WITHOUT auto-adding 7 days
            var wkId = await db.InsertWeeklyWorkloadTemplateAsync("Week Assign", "");

            // Create exactly one Wednesday and map the workload to it
            var wedTplId = await db.InsertDayWorkloadTemplateAsync("Wednesday", wkId);
            await db.AddWorkLoadTemplateToDayAsync(wedTplId, tpl);

            // Clone and read instances
            var start = new DateTime(2025, 1, 6);
            var instanceId = await db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkId, start, start.AddDays(6));
            var dayInstances = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(instanceId);
            var wedInst = dayInstances.First(d => d.day.Equals("Wednesday", StringComparison.OrdinalIgnoreCase));

            var loads = await db.GetWorkLoadInstancesForDayInstanceAsync(wedInst.dayWorkloadInstanceId);
            Assert.That(loads.Count, Is.GreaterThanOrEqualTo(1), "Wednesday instance should contain mapped workload(s).");

            var targetLoad = loads[0].workLoadInstanceId;
            await db.AssignEmployeeToWorkLoadInstanceAsync(empId, targetLoad);

            var resolved = await db.GetWorkLoadInstanceByIdAsync(targetLoad);
            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved?.workLoadTemplateName, Is.EqualTo("Close Store"));
        }
    }
}
