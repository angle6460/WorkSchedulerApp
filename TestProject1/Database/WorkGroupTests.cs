using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WorkGroupTests : DatabaseTestBase
    {
        [Test]
        public async Task InsertWorkGroupWithTemplates_CreatesGroupAndMappings()
        {
            var db = DatabaseHandler.Instance;

            var t1 = await db.InsertFixedWorkLoadTemplateAsync("Receiving", "Dock", 2);
            var t2 = await db.InsertFixedWorkLoadTemplateAsync("Facing", "Front shelves", 1);
            var groupId = await db.InsertWorkGroupWithTemplatesAsync("Back of House", new List<int> { t1, t2 });

            var groups = await db.GetAllWorkGroupsAsync();
            Assert.That(groups.Any(g => g.id == groupId), Is.True);

            var mapped = await db.GetWorkLoadTemplatesForGroupAsync(groupId);
            Assert.That(mapped.Select(m => m.workLoadTemplateId).ToHashSet().SetEquals(new[] { t1, t2 }), Is.True);
        }

        [Test]
        public async Task UpdateAndDeleteWorkGroup_Works()
        {
            var db = DatabaseHandler.Instance;
            var t1 = await db.InsertFixedWorkLoadTemplateAsync("Cashier", "POS", 1);
            var groupId = await db.InsertWorkGroupWithTemplatesAsync("Front of House", new() { t1 });

            await db.UpdateWorkGroupAsync(groupId, "Front of House+");
            var groups = await db.GetAllWorkGroupsAsync();
            Assert.That(groups.First(g => g.id == groupId).name, Is.EqualTo("Front of House+"));

            await db.DeleteWorkGroupAsync(groupId);
            var after = await db.GetAllWorkGroupsAsync();
            Assert.That(after.Any(g => g.id == groupId), Is.False);
        }
    }
}