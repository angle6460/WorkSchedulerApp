using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.Scheduling
{
    public static class ScheduleAutoAssigner
    {
        /// <summary>
        /// Automatically assigns qualified employees to workload instances for a given weekly workload instance.
        /// </summary>
        public static async Task<List<(string employeeId, int workLoadInstanceId)>> AssignEmployeesToWeeklyWorkloadInstanceAsync(
            DatabaseHandler db, int weeklyWorkloadInstanceId)
        {
            var assignments = new List<(string employeeId, int workLoadInstanceId)>();
            Console.WriteLine($"[AutoAssign] Starting assignment for weekly instance #{weeklyWorkloadInstanceId}");

            // 1️⃣ Get day instances for the specified weekly instance
            var dayInstances = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(weeklyWorkloadInstanceId);

            foreach (var (dayWorkloadInstanceId, dayName) in dayInstances)
            {
                // 2️⃣ Get all workload instances for this day
                var workLoadInstances = await db.GetWorkLoadInstancesForDayInstanceAsync(dayWorkloadInstanceId);

                foreach (var (workLoadInstanceId, workLoadTemplateId) in workLoadInstances)
                {
                    // 3️⃣ Find all employees skilled for this workload template
                    var qualifiedEmployees = await db.GetEmployeesForTemplateSkillAsync(workLoadTemplateId);

                    if (qualifiedEmployees.Count > 0)
                    {
                        // naive: assign the first qualified employee
                        var (employeeId, _) = qualifiedEmployees.First();

                        await db.AssignEmployeeToWorkLoadInstanceAsync(employeeId, workLoadInstanceId);
                        assignments.Add((employeeId, workLoadInstanceId));

                        Console.WriteLine(
                            $"[AutoAssign] Assigned {employeeId} to WorkLoadInstance {workLoadInstanceId} (template {workLoadTemplateId}) on {dayName}");
                    }
                    else
                    {
                        // Optional: Log the missing assignment with template info
                        var template = await db.GetWorkLoadTemplateByIdAsync(workLoadTemplateId);
                        var templateName = template?.name ?? $"Template#{workLoadTemplateId}";

                        Console.WriteLine($"[AutoAssign] No qualified employee for {templateName} on {dayName}");
                    }
                }
            }

            Console.WriteLine($"[AutoAssign] Completed assignments for instance #{weeklyWorkloadInstanceId}");
            return assignments;
        }
    }
}
