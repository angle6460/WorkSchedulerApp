using System;
using Microsoft.Data.Sqlite;
namespace WorkScedulerApp.Database;

public sealed class DatabaseHandler
{
    private static DatabaseHandler _instance;
    
    private static readonly object Lock = new object();
    
    private string _connectionString = @"Data Source=C:\Users\Angel\RiderProjects\WorkScedulerApp\Database\Database.db;";


    private DatabaseHandler()
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            
            SqliteCommand createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
-- ================================
-- WorkLoad Base Table (Polymorphic)
-- ================================
CREATE TABLE IF NOT EXISTS WorkLoad (
    WorkLoadID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    EstimatedHours INTEGER NOT NULL,
    WorkLoadType TEXT NOT NULL -- 'PerEmployee', 'PerItem', 'Fixed'
);

-- =====================================
-- WorkLoad Type-Specific Details Tables
-- =====================================
CREATE TABLE IF NOT EXISTS PerEmployeeWorkLoad (
    WorkLoadID INTEGER PRIMARY KEY,
    MinutesPerEmployee INTEGER NOT NULL,
    NumberOfEmployees INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS PerItemWorkLoad (
    WorkLoadID INTEGER PRIMARY KEY,
    MinutesPerItem INTEGER NOT NULL,
    NumberOfItems INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS FixedWorkLoad (
    WorkLoadID INTEGER PRIMARY KEY,
    FixedHours INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

-- ======================
-- WorkGroup and Mapping
-- ======================
CREATE TABLE IF NOT EXISTS WorkGroup (
    WorkGroupID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS WorkGroupWorkLoad (
    WorkGroupID INTEGER NOT NULL,
    WorkLoadID INTEGER NOT NULL,
    PRIMARY KEY (WorkGroupID, WorkLoadID),
    FOREIGN KEY (WorkGroupID) REFERENCES WorkGroup(WorkGroupID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

-- ==========
-- Employees
-- ==========
CREATE TABLE IF NOT EXISTS Employee (
    EmployeeID TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Role TEXT,
    RequestedHours INTEGER,
    Availability TEXT,
    ContractedHours TEXT
);

CREATE TABLE IF NOT EXISTS EmployeeSkills (
    EmployeeID TEXT NOT NULL,
    WorkLoadID INTEGER NOT NULL,
    PRIMARY KEY (EmployeeID, WorkLoadID),
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

-- ======
-- Shifts
-- ======
CREATE TABLE IF NOT EXISTS Shift (
    ShiftID INTEGER PRIMARY KEY AUTOINCREMENT,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NOT NULL
);

CREATE TABLE IF NOT EXISTS ShiftBreak (
    BreakID INTEGER PRIMARY KEY AUTOINCREMENT,
    ShiftID INTEGER NOT NULL,
    BreakTime DATETIME NOT NULL,
    FOREIGN KEY (ShiftID) REFERENCES Shift(ShiftID) ON DELETE CASCADE
);

-- ====================
-- Weekly Workload Template
-- ====================
CREATE TABLE IF NOT EXISTS WeeklyWorkload (
    WeeklyWorkloadID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT
);

CREATE TABLE IF NOT EXISTS DayWorkload (
    DayWorkloadID INTEGER PRIMARY KEY AUTOINCREMENT,
    Day TEXT NOT NULL,
    WeeklyWorkloadID INTEGER NOT NULL,
    FOREIGN KEY (WeeklyWorkloadID) REFERENCES WeeklyWorkload(WeeklyWorkloadID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS DayWorkloadWorkLoad (
    DayWorkloadID INTEGER NOT NULL,
    WorkLoadID INTEGER NOT NULL,
    PRIMARY KEY (DayWorkloadID, WorkLoadID),
    FOREIGN KEY (DayWorkloadID) REFERENCES DayWorkload(DayWorkloadID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

-- =====================
-- Actual Weekly Schedule
-- =====================
CREATE TABLE IF NOT EXISTS WeeklySchedule (
    WeeklyScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
    WeekStart DATE NOT NULL,
    WeekEnd DATE NOT NULL,
    WeeklyWorkloadID INTEGER,
    FOREIGN KEY (WeeklyWorkloadID) REFERENCES WeeklyWorkload(WeeklyWorkloadID)
);

CREATE TABLE IF NOT EXISTS EmployeeWeeklySchedule (
    WeeklyScheduleID INTEGER NOT NULL,
    EmployeeID TEXT NOT NULL,
    PRIMARY KEY (WeeklyScheduleID, EmployeeID),
    FOREIGN KEY (WeeklyScheduleID) REFERENCES WeeklySchedule(WeeklyScheduleID) ON DELETE CASCADE,
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS EmployeeDailySchedule (
    EmployeeDailyScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeID TEXT NOT NULL,
    WeeklyScheduleID INTEGER NOT NULL,
    DayWorkloadID INTEGER NOT NULL,
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
    FOREIGN KEY (WeeklyScheduleID) REFERENCES WeeklySchedule(WeeklyScheduleID) ON DELETE CASCADE,
    FOREIGN KEY (DayWorkloadID) REFERENCES DayWorkload(DayWorkloadID) ON DELETE CASCADE
);
";
            createTableCommand.ExecuteNonQuery();
        }
    }

    public static DatabaseHandler Instance
    {
        get
        {
            // Lock to ensure one thread can create the instance at a time
            lock (Lock)
            {
                if (_instance is null)
                {
                    _instance = new DatabaseHandler();
                }
                return _instance;
            }
        }
    }

    public string ConnectionString
    {
        get { return _connectionString; }
        set { _connectionString = value; }
    }
}