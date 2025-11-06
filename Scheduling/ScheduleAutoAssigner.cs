using System;
using System.Collections.Generic;
using System.Linq;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.Scheduling
{
    public static class ScheduleAutoAssigner
    {
        /// <summary>
        /// Automatically assigns employees to workloads for a given WeeklySchedule.
        /// Matches based on EmployeeSkill links (Employee ↔ WorkLoad).
        /// </summary>
        /// <param name="db">Database handler instance.</param>
        /// <param name="weeklyScheduleId">The target WeeklySchedule to assign.</param>
        /// <returns>List of (EmployeeID, WorkLoadID) assignments created.</returns>
        public static List<(string employeeId, int workLoadId)> AssignEmployeesToWeeklySchedule(DatabaseHandler db, int weeklyScheduleId)
        {
            var employees = db.GetAllEmployees();
            var skills = db.GetAllEmployeeSkills(); // (employeeId, workLoadId)
            var daySchedules = db.GetDaySchedulesForWeeklySchedule(weeklyScheduleId); // (dayScheduleId, dayName)
            var assignments = new List<(string, int)>();

            Console.WriteLine($"[AutoAssign] Starting assignment for schedule #{weeklyScheduleId}");

            foreach (var (dayScheduleId, dayName) in daySchedules)
            {
                var dayWorkloads = db.GetWorkLoadsForDaySchedule(dayScheduleId); // (workLoadId, name, type)

                foreach (var (wlId, wlName, wlType) in dayWorkloads)
                {
                    // Find employees who have a matching skill for this workload
                    var qualifiedEmployees = skills
                        .Where(s => s.workLoadId == wlId)
                        .Select(s => s.employeeId)
                        .ToList();

                    if (qualifiedEmployees.Count > 0)
                    {
                        // Pick first available employee (you can later balance by hours)
                        var employeeId = qualifiedEmployees.First();

                        assignments.Add((employeeId, wlId));
                        db.AssignEmployeeToWorkLoad(employeeId, wlId, dayScheduleId);

                        Console.WriteLine($"Assigned {employeeId} to {wlName} ({wlType}) on {dayName}");
                    }
                    else
                    {
                        Console.WriteLine($"[AutoAssign] No qualified employee for {wlName} ({wlType}) on {dayName}");
                    }
                }
            }

            Console.WriteLine($"[AutoAssign] Completed schedule #{weeklyScheduleId}");
            return assignments;
        }
    }
}
