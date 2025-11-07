using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class EmployeeCrudTests : DatabaseTestBase
    {
        [Test]
        public async Task InsertReadUpdateDeleteEmployee_WorksCorrectly()
        {
            var db = DatabaseHandler.Instance;
            var id = "EMP_" + Guid.NewGuid().ToString("N");

            // CREATE
            await db.InsertEmployeeAsync(id, "Alice", "Manager", 40, "Mon-Fri", "Full-Time");

            // READ
            var employees = await db.GetAllEmployeesAsync();
            Assert.That(employees.Any(e => e.id == id), Is.True);

            // UPDATE
            await db.UpdateEmployeeAsync(id, "Alice Smith", "Manager", 35, "Mon-Thu", "Part-Time");
            var updated = (await db.GetAllEmployeesAsync()).First(e => e.id == id);
            Assert.That(updated.name, Is.EqualTo("Alice Smith"));

            // DELETE
            await db.DeleteEmployeeAsync(id);
            var afterDelete = await db.GetAllEmployeesAsync();
            Assert.That(afterDelete.Any(e => e.id == id), Is.False);
        }

        [Test]
        public async Task DeletingEmployee_RemovesRelatedEmployeeSkills_Cascade()
        {
            var db = DatabaseHandler.Instance;
            var empId = "EMP_" + Guid.NewGuid().ToString("N");
            await db.InsertEmployeeAsync(empId, "Bob", "Staff", 30, "Mon-Fri", "Full-Time");

            var tplId = await db.InsertFixedWorkLoadTemplateAsync("Stocktake", "Monthly", 2.0);
            await db.AddTemplateSkillToEmployeeAsync(empId, tplId);

            Assert.That(await db.GetEmployeeTemplateSkillCountAsync(empId, tplId), Is.EqualTo(1));

            await db.DeleteEmployeeAsync(empId);
            Assert.That(await db.GetEmployeeTemplateSkillCountAsync(empId, tplId), Is.EqualTo(0));
        }
    }
}