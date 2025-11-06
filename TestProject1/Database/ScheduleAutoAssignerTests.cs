using NUnit.Framework;
using System;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Scheduling;

namespace WorkSchedulerApp.TestProject1.Database
{
    [TestFixture]
    public class ScheduleAutoAssignerTests : DatabaseTestBase
    {
        private DatabaseHandler db = null!;

        [SetUp]
        public void Setup()
        {
            db = DatabaseHandler.Instance;
        }

        [Test]
        public void AutoAssign_ShouldMatchEmployeesToWorkloads()
        {
            var alice = Guid.NewGuid().ToString();
            var bob = Guid.NewGuid().ToString();
            db.InsertEmployee(alice, "Alice", "Cashier", 20, "Mon–Fri", "Part-Time");
            db.InsertEmployee(bob, "Bob", "Stocker", 38, "Mon–Sun", "Full-Time");

            int wlRegister = db.InsertFixedWorkLoad("Register Shift", "POS", 6);
            int wlStock = db.InsertFixedWorkLoad("Shelf Restock", "Stocking", 8);
            int wlLift = db.InsertFixedWorkLoad("Heavy Lifting", "Warehouse", 10);

            db.AddSkillToEmployee(alice, wlRegister);
            db.AddSkillToEmployee(bob, wlStock);
            db.AddSkillToEmployee(bob, wlLift);

            int weeklyWorkloadId = db.InsertWeeklyWorkloadWithSevenDays("AutoAssign Template");
            int scheduleId = db.CloneWeeklyWorkloadToSchedule(weeklyWorkloadId, DateTime.Now.Date, DateTime.Now.Date.AddDays(6));

            var result = ScheduleAutoAssigner.AssignEmployeesToWeeklySchedule(db, scheduleId);


            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.Exists(x => x.employeeId == alice && x.workLoadId == wlRegister));
            Assert.That(result.Exists(x => x.employeeId == bob && x.workLoadId == wlStock));
            Assert.That(result.Exists(x => x.employeeId == bob && x.workLoadId == wlLift));
        }
    }
}