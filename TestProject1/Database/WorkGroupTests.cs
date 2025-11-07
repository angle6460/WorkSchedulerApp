using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    /// <summary>
    /// Composite (C1) WorkGroup tests validating:
    /// - Group templates as a first-class WorkLoadTemplate of type "Group"
    /// - Group <-> child mappings
    /// - Recursive hours computation
    /// - Expansion to leaf templates
    /// - Clone expansion into leaf WorkLoadInstances
    /// - Cascade behavior on delete
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WorkGroupTests : DatabaseTestBase
    {
        private DatabaseHandler Db => DatabaseHandler.Instance;

        // 1) Create a group workload template ---------------------------------
        [Test]
        public async Task CreateGroupTemplate_CreatesWorkLoadTemplateOfTypeGroup()
        {
            var groupId = await Db.InsertGroupWorkLoadTemplateAsync("Cleaning Group");
            Assert.That(groupId, Is.GreaterThan(0), "Group template id should be > 0.");

            var tpl = await Db.GetWorkLoadTemplateByIdAsync(groupId);
            Assert.That(tpl.HasValue, Is.True, "Template must exist after insert.");
            Assert.That(tpl?.type, Is.EqualTo("Group"), "WorkLoadType should be 'Group'.");
            Assert.That(tpl?.estimatedHours, Is.EqualTo(0).Within(0.0001), "Initial EstimatedHours for a group should be 0.");
        }

        // 2) Add child mapping -------------------------------------------------
        [Test]
        public async Task AddChildToGroup_AddsMappingToTable()
        {
            var g = await Db.InsertGroupWorkLoadTemplateAsync("Group A");
            var a = await Db.InsertFixedWorkLoadTemplateAsync("Leaf A", "desc", 1.0);

            await Db.AddChildToGroupAsync(g, a);

            var children = await Db.GetGroupChildrenAsync(g);
            Assert.That(children, Contains.Item(a));
        }

        // 3) Remove child mapping ----------------------------------------------
        [Test]
        public async Task RemoveChildFromGroup_RemovesMapping()
        {
            var g = await Db.InsertGroupWorkLoadTemplateAsync("Group A");
            var a = await Db.InsertFixedWorkLoadTemplateAsync("Leaf A", "", 1.0);

            await Db.AddChildToGroupAsync(g, a);
            var before = await Db.GetGroupChildrenAsync(g);
            Assert.That(before, Contains.Item(a), "Sanity: child must be present before removal.");

            await Db.RemoveChildFromGroupAsync(g, a);
            var after = await Db.GetGroupChildrenAsync(g);
            Assert.That(after, Does.Not.Contain(a), "Child should be removed.");
        }

        // 4) Recursive estimated hours (nested groups) -------------------------
        [Test]
        public async Task GetEstimatedHoursRecursive_ComputesSumForNestedGroups()
        {
            // G1 -> A (1h), G2
            // G2 -> B (2h), C (3h)
            // total = 6
            var g1 = await Db.InsertGroupWorkLoadTemplateAsync("G1");
            var g2 = await Db.InsertGroupWorkLoadTemplateAsync("G2");

            var a = await Db.InsertFixedWorkLoadTemplateAsync("A", "", 1.0);
            var b = await Db.InsertFixedWorkLoadTemplateAsync("B", "", 2.0);
            var c = await Db.InsertFixedWorkLoadTemplateAsync("C", "", 3.0);

            await Db.AddChildToGroupAsync(g1, a);
            await Db.AddChildToGroupAsync(g1, g2);

            await Db.AddChildToGroupAsync(g2, b);
            await Db.AddChildToGroupAsync(g2, c);

            var hours = await Db.GetEstimatedHoursRecursiveAsync(g1);
            Assert.That(hours, Is.EqualTo(6.0).Within(0.0001));
        }

        // 5) Expand to leaf templates -----------------------------------------
        [Test]
        public async Task ExpandToLeafTemplates_ReturnsOnlyLeaves()
        {
            // G1 -> A, G2; G2 -> B, C  => leaves = A, B, C
            var g1 = await Db.InsertGroupWorkLoadTemplateAsync("G1");
            var g2 = await Db.InsertGroupWorkLoadTemplateAsync("G2");

            var a = await Db.InsertFixedWorkLoadTemplateAsync("A", "", 1.0);
            var b = await Db.InsertFixedWorkLoadTemplateAsync("B", "", 2.0);
            var c = await Db.InsertFixedWorkLoadTemplateAsync("C", "", 3.0);

            await Db.AddChildToGroupAsync(g1, a);
            await Db.AddChildToGroupAsync(g1, g2);
            await Db.AddChildToGroupAsync(g2, b);
            await Db.AddChildToGroupAsync(g2, c);

            var leaves = await Db.ExpandToLeafTemplatesAsync(g1);
            Assert.That(leaves, Is.EquivalentTo(new[] { a, b, c }));
        }

        // 6) Clone expansion into leaf instances -------------------------------
        [Test]
        public async Task CloneWeeklyTemplate_ExpandsGroupsIntoLeafInstances()
        {
            // Setup leaf workloads
            var a = await Db.InsertFixedWorkLoadTemplateAsync("A", "", 1.0);
            var b = await Db.InsertFixedWorkLoadTemplateAsync("B", "", 1.0);

            // Group G -> { A, B }
            var g = await Db.InsertGroupWorkLoadTemplateAsync("G");
            await Db.AddChildToGroupAsync(g, a);
            await Db.AddChildToGroupAsync(g, b);

            // Weekly template with a single day referencing the GROUP, not leaves.
            var wkTpl = await Db.InsertWeeklyWorkloadTemplateAsync("WT1", "composite test");
            var monTpl = await Db.InsertDayWorkloadTemplateAsync("Monday", wkTpl);
            await Db.AddWorkLoadTemplateToDayAsync(monTpl, g);

            // Clone week; expansion should produce leaf instances for A and B
            var weekStart = new DateTime(2025, 1, 6);
            var weekEnd = weekStart.AddDays(6);
            var wkInstance = await Db.CloneWeeklyWorkloadTemplateToInstanceAsync(wkTpl, weekStart, weekEnd);

            // Gather all day instances then all workloads for those day instances
            var dayInstances = await Db.GetDayWorkloadInstancesForWeeklyInstanceAsync(wkInstance);
            var allWorkLoadInstances = new List<(int workLoadInstanceId, int workLoadTemplateId)>();
            foreach (var (dayInstId, _) in dayInstances)
            {
                var loads = await Db.GetWorkLoadInstancesForDayInstanceAsync(dayInstId);
                allWorkLoadInstances.AddRange(loads);
            }

            // Expect exactly the two leaves (A and B) and not the group G.
            Assert.That(allWorkLoadInstances.Count, Is.EqualTo(2), "Expected two leaf instances from the group.");
            Assert.That(allWorkLoadInstances.Any(x => x.workLoadTemplateId == a), Is.True, "Leaf A should be present.");
            Assert.That(allWorkLoadInstances.Any(x => x.workLoadTemplateId == b), Is.True, "Leaf B should be present.");
            Assert.That(allWorkLoadInstances.Any(x => x.workLoadTemplateId == g), Is.False, "Group itself should not appear as an instance.");
        }

        // 7) Cascade behavior on delete ---------------------------------------
        [Test]
        public async Task DeleteGroup_CascadesChildrenMappings()
        {
            var g = await Db.InsertGroupWorkLoadTemplateAsync("G");
            var w = await Db.InsertFixedWorkLoadTemplateAsync("W", "", 1.0);

            await Db.AddChildToGroupAsync(g, w);

            // Delete the group (parent). Children mappings should cascade away.
            await Db.DeleteWorkLoadTemplateAsync(g);

            var children = await Db.GetGroupChildrenAsync(g);
            Assert.That(children, Is.Empty, "Children mappings should be deleted via cascade.");
        }
    }
}
