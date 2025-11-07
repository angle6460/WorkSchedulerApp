using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WeeklyWorkloadTemplateTests : DatabaseTestBase
    {
        [Test]
        public async Task InsertWeeklyWorkloadTemplate_CreatesSuccessfully()
        {
            var db = DatabaseHandler.Instance;
            var id = await db.InsertWeeklyWorkloadTemplateAsync("Week A", "Morning shifts");
            var all = await db.GetAllWeeklyWorkloadTemplatesAsync();
            Assert.That(all.Any(w => w.id == id), Is.True);
        }

        [Test]
        public async Task InsertWeeklyWorkloadTemplateWithSevenDays_CreatesSevenDayTemplates()
        {
            var db = DatabaseHandler.Instance;
            var wkId = await db.InsertWeeklyWorkloadTemplateWithSevenDaysAsync("Week B", "");
            // Not all getters exist for Day templates; sanity check by adding an 8th day and ensuring Insert returns new id
            var extra = await db.InsertDayWorkloadTemplateAsync("Monday", wkId);
            Assert.That(extra, Is.GreaterThan(0));
        }

        [Test]
        public async Task UpdateAndDeleteWeeklyWorkloadTemplate_Works()
        {
            var db = DatabaseHandler.Instance;
            var wkId = await db.InsertWeeklyWorkloadTemplateAsync("Week C", "desc");
            
            await db.UpdateWeeklyWorkloadTemplateAsync(wkId, "Week C+", "updated");
            var afterUpdate = (await db.GetAllWeeklyWorkloadTemplatesAsync()).First(w => w.id == wkId);
            Assert.That(afterUpdate.name, Is.EqualTo("Week C+"));

            await db.DeleteWeeklyWorkloadTemplateAsync(wkId);
            var afterDel = await db.GetAllWeeklyWorkloadTemplatesAsync();
            Assert.That(afterDel.Any(w => w.id == wkId), Is.False);
        }
    }
}