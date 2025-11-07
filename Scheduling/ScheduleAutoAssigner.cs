using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.Scheduling
{
    public static class ScheduleAutoAssigner
    {
        private const double MaxHoursPerEmployeePerTask = 8.0; // can adjust or make configurable

        /// <summary>
        /// Automatically assigns employees to workload instances for a given weekly workload instance.
        /// Takes into account: contracted hours, requested hours, load balancing, multi-employee tasks.
        /// </summary>
        public static async Task<List<(string employeeId, int workLoadInstanceId)>> AssignEmployeesToWeeklyWorkloadInstanceAsync(
            DatabaseHandler db, int weeklyWorkloadInstanceId)
        {
            var assignments = new List<(string employeeId, int workLoadInstanceId)>();

            Console.WriteLine($"[AutoAssign] Starting assignment for weekly instance #{weeklyWorkloadInstanceId}");

            // Get all day instances
            var dayInstances = await db.GetDayWorkloadInstancesForWeeklyInstanceAsync(weeklyWorkloadInstanceId);

            foreach (var (dayWorkloadInstanceId, dayName) in dayInstances)
            {
                // Get all workload instances for this day
                var workLoadInstances = await db.GetWorkLoadInstancesForDayInstanceAsync(dayWorkloadInstanceId);

                foreach (var (workLoadInstanceId, workLoadTemplateId) in workLoadInstances)
                {
                    // Load template info
                    var template = await db.GetWorkLoadTemplateByIdAsync(workLoadTemplateId);
                    if (template is null) continue;

                    double totalHours = template.Value.estimatedHours;

                    // Determine how many employees should share this workload
                    int employeesNeeded = Math.Max(
                        1, 
                        (int)Math.Ceiling(totalHours / MaxHoursPerEmployeePerTask)
                    );

                    // Get ranked employees based on contracted/requested hours logic
                    var ranked = await RankEmployeesForTask(db, workLoadTemplateId, weeklyWorkloadInstanceId);

                    if (ranked.Count == 0)
                    {
                        Console.WriteLine($"[AutoAssign] No qualified employees for template {workLoadTemplateId} on {dayName}");
                        continue;
                    }

                    Console.WriteLine($"[AutoAssign] Assigning up to {employeesNeeded} employees for task {workLoadInstanceId} ({totalHours}h)");

                    int assigned = 0;

                    // Assign top N employees
                    foreach (var rankedEntry in ranked)
                    {
                        if (assigned >= employeesNeeded)
                            break;

                        string employeeId = rankedEntry.employeeId;

                        // Make the assignment
                        await db.AssignEmployeeToWorkLoadInstanceAsync(employeeId, workLoadInstanceId);

                        assignments.Add((employeeId, workLoadInstanceId));

                        Console.WriteLine(
                            $"[AutoAssign] Assigned {employeeId} to WorkLoadInstance {workLoadInstanceId} ({template?.name}) on {dayName}"
                        );

                        assigned++;
                    }
                }
            }

            Console.WriteLine($"[AutoAssign] Completed assignments for instance #{weeklyWorkloadInstanceId}");
            return assignments;
        }

        // --------------------------------------------------------------------
        //  Ranking Logic
        // --------------------------------------------------------------------
        //
        // Priority Rules:
        // 1) Employees BELOW contracted hours → MUST get hours first
        // 2) Employees BETWEEN contracted and requested → next priority
        // 3) Employees ABOVE requested → lowest priority
        //
        // Resulting score:
        // Priority 1: 1000 + remainingToContracted
        // Priority 2: 500 + remainingToRequested
        // Priority 3: 10 - overload
        //
        // Higher score = higher priority.
        // --------------------------------------------------------------------
        private static async Task<List<(string employeeId, double score)>> RankEmployeesForTask(
            DatabaseHandler db,
            int workLoadTemplateId,
            int weeklyInstanceId)
        {
            // Get all skilled employees
            var qualified = await db.GetEmployeesForTemplateSkillAsync(workLoadTemplateId);

            var scores = new List<(string employeeId, double score)>();

            foreach (var (employeeId, _) in qualified)
            {
                var emp = (await db.GetAllEmployeesAsync()).First(e => e.id == employeeId);

                double contracted = double.Parse(emp.contractedHours);   // from TEXT → double
                double requested  = emp.requestedHours;                  // already an int


                // Assigned hours this week
                double assigned = await db.SumAssignedHoursForEmployeeInWeek(employeeId, weeklyInstanceId);

                double remainingToContracted = Math.Max(0, contracted - assigned);
                double remainingToRequested  = Math.Max(0, requested - assigned);

                double priorityScore;

                if (assigned < contracted)
                {
                    // Priority 1 — MUST get hours
                    priorityScore = 1000 + remainingToContracted;
                }
                else if (assigned < requested)
                {
                    // Priority 2 — TRY to give hours
                    priorityScore = 500 + remainingToRequested;
                }
                else
                {
                    // Priority 3 — employees above requested hours
                    double overload = assigned - requested;
                    priorityScore = 10 - overload; // lower score = lower priority
                }

                scores.Add((employeeId, priorityScore));
            }

            // Higher score = better priority
            return scores
                .OrderByDescending(x => x.score)
                .ToList();
        }
    }
}
