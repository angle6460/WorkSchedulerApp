using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WorkLoadTemplateTests : DatabaseTestBase
    {
        [Test]
        public async Task InsertPerEmployeeWorkLoadTemplate_CalculatesEstimatedHours()
        {
            var db = DatabaseHandler.Instance;
            var id = await db.InsertPerEmployeeWorkLoadTemplateAsync("Training", "Onboard", 30, 4); // 2 hours
            var tpl = await db.GetWorkLoadTemplateByIdAsync(id);
            Assert.That(tpl?.estimatedHours, Is.EqualTo(2.0).Within(0.0001));
            Assert.That(tpl?.type, Is.EqualTo("PerEmployee"));
        }

        [Test]
        public async Task InsertPerItemWorkLoadTemplate_CalculatesEstimatedHours()
        {
            var db = DatabaseHandler.Instance;
            var id = await db.InsertPerItemWorkLoadTemplateAsync("Pack Orders", "Daily", 5, 24); // 2 hours
            var tpl = await db.GetWorkLoadTemplateByIdAsync(id);
            Assert.That(tpl?.estimatedHours, Is.EqualTo(2.0).Within(0.0001));
            Assert.That(tpl?.type, Is.EqualTo("PerItem"));
        }

        [Test]
        public async Task InsertFixedWorkLoadTemplate_CreatesFixedType()
        {
            var db = DatabaseHandler.Instance;
            var id = await db.InsertFixedWorkLoadTemplateAsync("Cleaning", "Daily clean", 1.5);
            var tpl = await db.GetWorkLoadTemplateByIdAsync(id);
            Assert.That(tpl?.estimatedHours, Is.EqualTo(1.5).Within(0.0001));
            Assert.That(tpl?.type, Is.EqualTo("Fixed"));
        }

        [Test]
        public async Task UpdateAndDeleteWorkLoadTemplate_WorksAndCascades()
        {
            var db = DatabaseHandler.Instance;
            var id = await db.InsertFixedWorkLoadTemplateAsync("Restock", "Shelves", 2.0);

            await db.UpdateWorkLoadTemplateAsync(id, "Restock+", "Shelves & backroom", 2.5);
            var updated = await db.GetWorkLoadTemplateByIdAsync(id);
            Assert.That(updated?.name, Is.EqualTo("Restock+"));

            await db.DeleteWorkLoadTemplateAsync(id);
            var after = await db.GetWorkLoadTemplateByIdAsync(id);
            Assert.That(after, Is.Null);
        }
    }
}
