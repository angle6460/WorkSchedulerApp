using Microsoft.Data.Sqlite;

namespace WorkSchedulerApp.Database;

public sealed class DatabaseHandler
{
    private static DatabaseHandler? _instance;
    private static readonly object Lock = new();

    // Default is UNKNOWN so apps/tests must Initialize(...) explicitly.
    private string _connectionString = @"UNKNOWN";

    private DatabaseHandler()
    {
        // EnsureSchema();  // Deferred until ConnectionString is set
    }

    public static DatabaseHandler Instance
    {
        get
        {
            lock (Lock)
            {
                _instance ??= new DatabaseHandler();
                return _instance;
            }
        }
    }

    public DatabaseHandler Initialize(string connectionString)
    {
        ConnectionString = connectionString; // triggers EnsureSchema()
        return this;
    }

    public string ConnectionString
    {
        get => _connectionString;
        set
        {
            _connectionString = value;
            EnsureSchema();
        }
    }

    private SqliteConnection OpenConnection()
    {
        if (_connectionString == "UNKNOWN")
            throw new InvalidOperationException("Cannot connect to SQL Server");

        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // Enforce foreign keys
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        return conn;
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
-- =====================================================================
-- Core WorkLoad (polymorphic root) + Subtype Tables (Table-per-Subclass)
-- =====================================================================
CREATE TABLE IF NOT EXISTS WorkLoad (
WorkLoadID INTEGER PRIMARY KEY AUTOINCREMENT,
Name TEXT NOT NULL,
Description TEXT,
EstimatedHours INTEGER NOT NULL,
WorkLoadType TEXT NOT NULL
);

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
-- WorkGroup + Mapping
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

-- ========== Employees ==========
CREATE TABLE IF NOT EXISTS Employee (
EmployeeID TEXT PRIMARY KEY,
Name TEXT NOT NULL,
Role TEXT,
RequestedHours INTEGER,
Availability TEXT,
ContractedHours TEXT
);

-- =========================================
-- Skills = direct link Employee ↔ WorkLoad
-- =========================================
CREATE TABLE IF NOT EXISTS EmployeeSkill (
EmployeeID TEXT NOT NULL,
WorkLoadID INTEGER NOT NULL,
PRIMARY KEY (EmployeeID, WorkLoadID),
FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

-- ====== Shifts & Breaks ======
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

-- ==============================
-- Weekly Workload (Template)
-- ==============================
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
-- Weekly Schedule (Actual)
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

-- =====================
-- Day Schedule (Actual calendar day entries linked to weekly schedule)
-- =====================
CREATE TABLE IF NOT EXISTS DaySchedule (
DayScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
WeeklyScheduleID INTEGER NOT NULL,
ScheduleDate DATE NOT NULL,
DayWorkloadID INTEGER,
FOREIGN KEY (WeeklyScheduleID) REFERENCES WeeklySchedule(WeeklyScheduleID) ON DELETE CASCADE,
FOREIGN KEY (DayWorkloadID) REFERENCES DayWorkload(DayWorkloadID) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS EmployeeWorkLoadAssignment (
    AssignmentID INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeID TEXT NOT NULL,
    DayScheduleID INTEGER NOT NULL,
    WorkLoadID INTEGER NOT NULL,
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
    FOREIGN KEY (DayScheduleID) REFERENCES DaySchedule(DayScheduleID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadID) REFERENCES WorkLoad(WorkLoadID) ON DELETE CASCADE
);

";
        cmd.ExecuteNonQuery();
    }

    public void Close()
    {
        SqliteConnection.ClearAllPools();
    }

    // =====================================================================
    // WorkLoad Inserts (return ids for tests/logic)
    // =====================================================================

    public int InsertPerEmployeeWorkLoad(string name, string description, int minutesPerEmployee, int numberOfEmployees)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var insertWorkLoad = connection.CreateCommand();
            insertWorkLoad.CommandText = @"
                INSERT INTO WorkLoad (Name, Description, EstimatedHours, WorkLoadType)
                VALUES ($name, $description, $estimatedHours, 'PerEmployee');
                SELECT last_insert_rowid();";
            insertWorkLoad.Parameters.AddWithValue("$name", name);
            insertWorkLoad.Parameters.AddWithValue("$description", description);
            insertWorkLoad.Parameters.AddWithValue("$estimatedHours", (minutesPerEmployee * numberOfEmployees) / 60);
            long workLoadId = (long)insertWorkLoad.ExecuteScalar();

            var insertDetails = connection.CreateCommand();
            insertDetails.CommandText = @"
                INSERT INTO PerEmployeeWorkLoad (WorkLoadID, MinutesPerEmployee, NumberOfEmployees)
                VALUES ($id, $minutesPerEmployee, $numberOfEmployees);";
            insertDetails.Parameters.AddWithValue("$id", workLoadId);
            insertDetails.Parameters.AddWithValue("$minutesPerEmployee", minutesPerEmployee);
            insertDetails.Parameters.AddWithValue("$numberOfEmployees", numberOfEmployees);
            insertDetails.ExecuteNonQuery();

            transaction.Commit();
            return (int)workLoadId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public int InsertPerItemWorkLoad(string name, string description, int minutesPerItem, int numberOfItems)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var insertWorkLoad = connection.CreateCommand();
            insertWorkLoad.CommandText = @"
                INSERT INTO WorkLoad (Name, Description, EstimatedHours, WorkLoadType)
                VALUES ($name, $description, $estimatedHours, 'PerItem');
                SELECT last_insert_rowid();";
            insertWorkLoad.Parameters.AddWithValue("$name", name);
            insertWorkLoad.Parameters.AddWithValue("$description", description);
            insertWorkLoad.Parameters.AddWithValue("$estimatedHours", (minutesPerItem * numberOfItems) / 60);
            long workLoadId = (long)insertWorkLoad.ExecuteScalar();

            var insertDetails = connection.CreateCommand();
            insertDetails.CommandText = @"
                INSERT INTO PerItemWorkLoad (WorkLoadID, MinutesPerItem, NumberOfItems)
                VALUES ($id, $minutesPerItem, $numberOfItems);";
            insertDetails.Parameters.AddWithValue("$id", workLoadId);
            insertDetails.Parameters.AddWithValue("$minutesPerItem", minutesPerItem);
            insertDetails.Parameters.AddWithValue("$numberOfItems", numberOfItems);
            insertDetails.ExecuteNonQuery();

            transaction.Commit();
            return (int)workLoadId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public int InsertFixedWorkLoad(string name, string description, int fixedHours)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var insertWorkLoad = connection.CreateCommand();
            insertWorkLoad.CommandText = @"
                INSERT INTO WorkLoad (Name, Description, EstimatedHours, WorkLoadType)
                VALUES ($name, $description, $estimatedHours, 'Fixed');
                SELECT last_insert_rowid();";
            insertWorkLoad.Parameters.AddWithValue("$name", name);
            insertWorkLoad.Parameters.AddWithValue("$description", description);
            insertWorkLoad.Parameters.AddWithValue("$estimatedHours", fixedHours);
            long workLoadId = (long)insertWorkLoad.ExecuteScalar();

            var insertDetails = connection.CreateCommand();
            insertDetails.CommandText = @"
                INSERT INTO FixedWorkLoad (WorkLoadID, FixedHours)
                VALUES ($id, $fixedHours);";
            insertDetails.Parameters.AddWithValue("$id", workLoadId);
            insertDetails.Parameters.AddWithValue("$fixedHours", fixedHours);
            insertDetails.ExecuteNonQuery();

            transaction.Commit();
            return (int)workLoadId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =====================================================================
    // WorkGroup
    // =====================================================================

    public int InsertWorkGroup(string name, List<int> workLoadIds)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var insertGroup = connection.CreateCommand();
            insertGroup.CommandText = @"
                INSERT INTO WorkGroup (Name)
                VALUES ($name);
                SELECT last_insert_rowid();";
            insertGroup.Parameters.AddWithValue("$name", name);
            long workGroupId = (long)insertGroup.ExecuteScalar();

            foreach (var id in workLoadIds)
            {
                var mapCmd = connection.CreateCommand();
                mapCmd.CommandText = @"
                    INSERT INTO WorkGroupWorkLoad (WorkGroupID, WorkLoadID)
                    VALUES ($groupId, $workLoadId);";
                mapCmd.Parameters.AddWithValue("$groupId", workGroupId);
                mapCmd.Parameters.AddWithValue("$workLoadId", id);
                mapCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return (int)workGroupId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void UpdateWorkGroup(int workGroupId, string newName)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE WorkGroup
                SET Name = $name
                WHERE WorkGroupID = $id;";
            cmd.Parameters.AddWithValue("$id", workGroupId);
            cmd.Parameters.AddWithValue("$name", newName);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteWorkGroup(int workGroupId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM WorkGroup WHERE WorkGroupID = $id;";
            cmd.Parameters.AddWithValue("$id", workGroupId);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =====================================================================
    // Employees
    // =====================================================================

    public void InsertEmployee(string employeeId, string name, string role, int requestedHours, string availability, string contractedHours)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Employee (EmployeeID, Name, Role, RequestedHours, Availability, ContractedHours)
                VALUES ($id, $name, $role, $requested, $availability, $contracted);";
            cmd.Parameters.AddWithValue("$id", employeeId);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$role", role);
            cmd.Parameters.AddWithValue("$requested", requestedHours);
            cmd.Parameters.AddWithValue("$availability", availability);
            cmd.Parameters.AddWithValue("$contracted", contractedHours);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void UpdateEmployee(string employeeId, string name, string role, int requestedHours, string availability, string contractedHours)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Employee
                SET 
                    Name = $name,
                    Role = $role,
                    RequestedHours = $requested,
                    Availability = $availability,
                    ContractedHours = $contracted
                WHERE EmployeeID = $id;";
            cmd.Parameters.AddWithValue("$id", employeeId);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$role", role);
            cmd.Parameters.AddWithValue("$requested", requestedHours);
            cmd.Parameters.AddWithValue("$availability", availability);
            cmd.Parameters.AddWithValue("$contracted", contractedHours);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteEmployee(string employeeId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Employee WHERE EmployeeID = $id;";
            cmd.Parameters.AddWithValue("$id", employeeId);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<(string id, string name, string role)> GetAllEmployees()
    {
        var result = new List<(string, string, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmployeeID, Name, Role FROM Employee;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var name = reader.GetString(1);
            var role = reader.IsDBNull(2) ? "" : reader.GetString(2);
            result.Add((id, name, role));
        }
        return result;
    }

    public (string id, string name, string role, int requestedHours, string availability, string contractedHours)? GetEmployeeById(string employeeId)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT EmployeeID, Name, Role, RequestedHours, Availability, ContractedHours
            FROM Employee
            WHERE EmployeeID = $id;";
        cmd.Parameters.AddWithValue("$id", employeeId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4),
                reader.IsDBNull(5) ? "" : reader.GetString(5)
            );
        }
        return null;
    }

    // =====================================================================
    // Employee ↔ WorkLoad skills (link table)
    // =====================================================================

    public void AddSkillToEmployee(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO EmployeeSkill (EmployeeID, WorkLoadID)
            VALUES ($emp, $wl);";
        cmd.Parameters.AddWithValue("$emp", employeeId);
        cmd.Parameters.AddWithValue("$wl", workLoadId);
        cmd.ExecuteNonQuery();
    }

    public void RemoveSkillFromEmployee(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM EmployeeSkill
            WHERE EmployeeID = $emp AND WorkLoadID = $wl;";
        cmd.Parameters.AddWithValue("$emp", employeeId);
        cmd.Parameters.AddWithValue("$wl", workLoadId);
        cmd.ExecuteNonQuery();
    }

    public List<(string employeeId, int workLoadId)> GetAllEmployeeSkills()
    {
        var list = new List<(string, int)>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmployeeID, WorkLoadID FROM EmployeeSkill;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetString(0), reader.GetInt32(1)));
        return list;
    }

    public List<(int workLoadId, string workLoadName)> GetSkillsForEmployee(string employeeId)
    {
        var result = new List<(int, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT wl.WorkLoadID, wl.Name
            FROM EmployeeSkill es
            JOIN WorkLoad wl ON wl.WorkLoadID = es.WorkLoadID
            WHERE es.EmployeeID = $id;";
        cmd.Parameters.AddWithValue("$id", employeeId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        return result;
    }

    public List<string> GetEmployeesSkilledForWorkLoad(int workLoadId)
    {
        var list = new List<string>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmployeeID FROM EmployeeSkill WHERE WorkLoadID = $w;";
        cmd.Parameters.AddWithValue("$w", workLoadId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    // =====================================================================
    // Queries (WorkLoad, WorkGroup, etc.)
    // =====================================================================

    public List<(int id, string name, string type)> GetAllWorkLoads()
    {
        var result = new List<(int, string, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkLoadID, Name, WorkLoadType FROM WorkLoad;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return result;
    }

    public (string name, string description, int estimatedHours, string type)? GetWorkLoadById(int id)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Name, Description, EstimatedHours, WorkLoadType
            FROM WorkLoad
            WHERE WorkLoadID = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (
                reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3)
            );
        }
        return null;
    }

    public List<(int id, string name)> GetAllWorkGroups()
    {
        var result = new List<(int, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkGroupID, Name FROM WorkGroup;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        }
        return result;
    }

    public List<(int workLoadId, string workLoadName)> GetWorkLoadsForGroup(int workGroupId)
    {
        var result = new List<(int, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT wl.WorkLoadID, wl.Name
            FROM WorkLoad wl
            JOIN WorkGroupWorkLoad gwl ON wl.WorkLoadID = gwl.WorkLoadID
            WHERE gwl.WorkGroupID = $groupId;";
        cmd.Parameters.AddWithValue("$groupId", workGroupId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        return result;
    }

    public int GetWorkLoadIdByName(string name)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name=$name;";
        cmd.Parameters.AddWithValue("$name", name);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : -1;
    }

    public int GetWorkLoadCountByName(string name)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WorkLoad WHERE Name=$name;";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // =====================================================================
    // Updates / Deletes (WorkLoad)
    // =====================================================================

    public void UpdateWorkLoad(int workLoadId, string name, string description, int estimatedHours)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE WorkLoad
                SET 
                    Name = $name,
                    Description = $description,
                    EstimatedHours = $hours
                WHERE WorkLoadID = $id;";
            cmd.Parameters.AddWithValue("$id", workLoadId);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$description", description);
            cmd.Parameters.AddWithValue("$hours", estimatedHours);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void DeleteWorkLoad(int workLoadId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM WorkLoad WHERE WorkLoadID = $id;";
            cmd.Parameters.AddWithValue("$id", workLoadId);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =====================================================================
    // Shifts & Breaks
    // =====================================================================

    public int InsertShift(DateTime startTime, DateTime endTime)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Shift (StartTime, EndTime)
                VALUES ($start, $end);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$start", startTime);
            cmd.Parameters.AddWithValue("$end", endTime);
            long shiftId = (long)cmd.ExecuteScalar();

            transaction.Commit();
            return (int)shiftId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void AddBreakToShift(int shiftId, DateTime breakTime)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ShiftBreak (ShiftID, BreakTime)
                VALUES ($shiftId, $breakTime);";
            cmd.Parameters.AddWithValue("$shiftId", shiftId);
            cmd.Parameters.AddWithValue("$breakTime", breakTime);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public (int id, DateTime start, DateTime end, List<DateTime> breaks)? GetShiftById(int shiftId)
    {
        using var connection = OpenConnection();

        var shiftCmd = connection.CreateCommand();
        shiftCmd.CommandText = "SELECT ShiftID, StartTime, EndTime FROM Shift WHERE ShiftID = $id;";
        shiftCmd.Parameters.AddWithValue("$id", shiftId);

        using var reader = shiftCmd.ExecuteReader();
        if (!reader.Read()) return null;

        var id = reader.GetInt32(0);
        var start = reader.GetDateTime(1);
        var end = reader.GetDateTime(2);

        var breaks = new List<DateTime>();
        var breakCmd = connection.CreateCommand();
        breakCmd.CommandText = "SELECT BreakTime FROM ShiftBreak WHERE ShiftID = $id;";
        breakCmd.Parameters.AddWithValue("$id", shiftId);
        using var breakReader = breakCmd.ExecuteReader();
        while (breakReader.Read())
            breaks.Add(breakReader.GetDateTime(0));

        return (id, start, end, breaks);
    }

    public List<(int id, DateTime start, DateTime end)> GetAllShifts()
    {
        var result = new List<(int, DateTime, DateTime)>();
        using var connection = OpenConnection();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ShiftID, StartTime, EndTime FROM Shift;";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
            result.Add((reader.GetInt32(0), reader.GetDateTime(1), reader.GetDateTime(2)));

        return result;
    }

    public void DeleteShift(int shiftId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Shift WHERE ShiftID = $id;";
            cmd.Parameters.AddWithValue("$id", shiftId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =====================================================================
    // Weekly Workload Templates / DayWorkloads
    // =====================================================================

    public int InsertWeeklyWorkloadTemplate(string name, string description)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WeeklyWorkload (Name, Description)
            VALUES ($name, $desc);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$desc", description);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int InsertDayWorkload(string day, int weeklyWorkloadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DayWorkload (Day, WeeklyWorkloadID)
            VALUES ($day, $wkId);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$day", day);
        cmd.Parameters.AddWithValue("$wkId", weeklyWorkloadId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void LinkDayWorkloadToWorkLoad(int dayWorkloadId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO DayWorkloadWorkLoad (DayWorkloadID, WorkLoadID)
            VALUES ($d, $w);";
        cmd.Parameters.AddWithValue("$d", dayWorkloadId);
        cmd.Parameters.AddWithValue("$w", workLoadId);
        cmd.ExecuteNonQuery();
    }

    // Clone template day workloads + mappings to a new WeeklySchedule
    // NOTE: The cloned DayWorkloads remain part of the template table 
    // and the WeeklySchedule references the source WeeklyWorkload. DaySchedule can then map dates.
   public int CloneWeeklyWorkloadToSchedule(int weeklyWorkloadId, DateTime weekStart, DateTime weekEnd)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // STEP 1: Create new WeeklySchedule
            var insertSchedule = connection.CreateCommand();
            insertSchedule.CommandText = @"
                INSERT INTO WeeklySchedule (WeekStart, WeekEnd, WeeklyWorkloadID)
                VALUES ($start, $end, $template);
                SELECT last_insert_rowid();";
            insertSchedule.Parameters.AddWithValue("$start", weekStart);
            insertSchedule.Parameters.AddWithValue("$end", weekEnd);
            insertSchedule.Parameters.AddWithValue("$template", weeklyWorkloadId);
            long newScheduleId = (long)insertSchedule.ExecuteScalar();

            // STEP 2: Read existing DayWorkloads for the template
            var getDays = connection.CreateCommand();
            getDays.CommandText = @"
                SELECT DayWorkloadID, Day
                FROM DayWorkload
                WHERE WeeklyWorkloadID = $wk;";
            getDays.Parameters.AddWithValue("$wk", weeklyWorkloadId);

            var daysToClone = new List<(int oldId, string dayName)>();
            using (var reader = getDays.ExecuteReader())
            {
                while (reader.Read())
                    daysToClone.Add((reader.GetInt32(0), reader.GetString(1)));
            }

            // STEP 3: Deep-copy DayWorkloads for the new schedule
            var newDayMap = new Dictionary<int, int>();
            foreach (var (oldDayId, dayName) in daysToClone)
            {
                var insertDay = connection.CreateCommand();
                insertDay.CommandText = @"
                    INSERT INTO DayWorkload (Day, WeeklyWorkloadID)
                    VALUES ($day, $wk);
                    SELECT last_insert_rowid();";
                insertDay.Parameters.AddWithValue("$day", dayName);
                insertDay.Parameters.AddWithValue("$wk", weeklyWorkloadId);
                int newDayId = Convert.ToInt32(insertDay.ExecuteScalar());
                newDayMap[oldDayId] = newDayId;
            }

            // STEP 4: Clone WorkLoads linked to each DayWorkload
            foreach (var pair in newDayMap)
            {
                var getWorkloads = connection.CreateCommand();
                getWorkloads.CommandText = @"
                    SELECT w.WorkLoadID, w.Name, w.Description, w.EstimatedHours, w.WorkLoadType
                    FROM WorkLoad w
                    JOIN DayWorkloadWorkLoad dww ON w.WorkLoadID = dww.WorkLoadID
                    WHERE dww.DayWorkloadID = $oldId;";
                getWorkloads.Parameters.AddWithValue("$oldId", pair.Key);

                using var reader = getWorkloads.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader.GetString(1);
                    string description = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    int hours = reader.GetInt32(3);
                    string type = reader.GetString(4);

                    // Insert cloned workload (no BaseWorkLoadID)
                    var insertWorkLoad = connection.CreateCommand();
                    insertWorkLoad.CommandText = @"
                        INSERT INTO WorkLoad (Name, Description, EstimatedHours, WorkLoadType)
                        VALUES ($name, $desc, $hours, $type);
                        SELECT last_insert_rowid();";
                    insertWorkLoad.Parameters.AddWithValue("$name", name);
                    insertWorkLoad.Parameters.AddWithValue("$desc", description);
                    insertWorkLoad.Parameters.AddWithValue("$hours", hours);
                    insertWorkLoad.Parameters.AddWithValue("$type", type);
                    int newWorkLoadId = Convert.ToInt32(insertWorkLoad.ExecuteScalar());

                    // Link new workload to the new DayWorkload
                    var linkCmd = connection.CreateCommand();
                    linkCmd.CommandText = @"
                        INSERT INTO DayWorkloadWorkLoad (DayWorkloadID, WorkLoadID)
                        VALUES ($day, $wl);";
                    linkCmd.Parameters.AddWithValue("$day", pair.Value);
                    linkCmd.Parameters.AddWithValue("$wl", newWorkLoadId);
                    linkCmd.ExecuteNonQuery();
                }
            }

            // STEP 5: Always create exactly 7 DaySchedules (Mon–Sun)
            string[] allDays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            for (int i = 0; i < allDays.Length; i++)
            {
                DateTime date = weekStart.AddDays(i);
                string weekdayName = allDays[i];

                // Try to match a cloned DayWorkload for that weekday
                int? matchedId = newDayMap
                    .Where(d => daysToClone.Any(o =>
                        o.oldId == d.Key &&
                        string.Equals(o.dayName, weekdayName, StringComparison.OrdinalIgnoreCase)))
                    .Select(d => (int?)d.Value)
                    .FirstOrDefault();

                var insertDaySchedule = connection.CreateCommand();
                insertDaySchedule.CommandText = @"
                    INSERT INTO DaySchedule (WeeklyScheduleID, ScheduleDate, DayWorkloadID)
                    VALUES ($sched, $date, $dayId);";
                insertDaySchedule.Parameters.AddWithValue("$sched", newScheduleId);
                insertDaySchedule.Parameters.AddWithValue("$date", date);
                insertDaySchedule.Parameters.AddWithValue("$dayId", (object?)matchedId ?? DBNull.Value);
                insertDaySchedule.ExecuteNonQuery();
            }

            transaction.Commit();
            return (int)newScheduleId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }



    private static DateTime ResolveDateForDayName(DateTime weekStart, string dayName)
    {
        // Simple resolver: assumes weekStart is the Monday of that week; adjust as you prefer.
        // Supports typical English day names; unrecognized -> use weekStart.
        var baseMonday = weekStart.Date; // treat as start of week
        string dn = (dayName ?? "").Trim().ToLowerInvariant();
        int offset = dn switch
        {
            "monday" => 0,
            "tuesday" => 1,
            "wednesday" => 2,
            "thursday" => 3,
            "friday" => 4,
            "saturday" => 5,
            "sunday" => 6,
            _ => 0
        };
        return baseMonday.AddDays(offset);
    }

    // =====================================================================
    // Weekly Schedule (Actual) + Assignments + DaySchedule
    // =====================================================================

    public int InsertWeeklySchedule(DateTime weekStart, DateTime weekEnd, int? weeklyWorkloadId = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO WeeklySchedule (WeekStart, WeekEnd, WeeklyWorkloadID)
                VALUES ($start, $end, $template);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$start", weekStart);
            cmd.Parameters.AddWithValue("$end", weekEnd);
            cmd.Parameters.AddWithValue("$template", (object?)weeklyWorkloadId ?? DBNull.Value);

            long scheduleId = (long)cmd.ExecuteScalar();
            transaction.Commit();
            return (int)scheduleId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void AssignEmployeeToSchedule(int weeklyScheduleId, string employeeId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO EmployeeWeeklySchedule (WeeklyScheduleID, EmployeeID)
                VALUES ($schedule, $employee);";
            cmd.Parameters.AddWithValue("$schedule", weeklyScheduleId);
            cmd.Parameters.AddWithValue("$employee", employeeId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void AssignEmployeeToDayWorkload(string employeeId, int weeklyScheduleId, int dayWorkloadId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO EmployeeDailySchedule (EmployeeID, WeeklyScheduleID, DayWorkloadID)
                VALUES ($employee, $schedule, $day);";
            cmd.Parameters.AddWithValue("$employee", employeeId);
            cmd.Parameters.AddWithValue("$schedule", weeklyScheduleId);
            cmd.Parameters.AddWithValue("$day", dayWorkloadId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<(int id, DateTime start, DateTime end, int? workloadTemplateId)> GetAllWeeklySchedules()
    {
        var result = new List<(int, DateTime, DateTime, int?)>();
        using var connection = OpenConnection();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WeeklyScheduleID, WeekStart, WeekEnd, WeeklyWorkloadID FROM WeeklySchedule;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetInt32(0),
                reader.GetDateTime(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)
            ));
        }

        return result;
    }

    public List<string> GetEmployeesForSchedule(int weeklyScheduleId)
    {
        var result = new List<string>();
        using var connection = OpenConnection();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT e.Name
            FROM Employee e
            JOIN EmployeeWeeklySchedule es ON e.EmployeeID = es.EmployeeID
            WHERE es.WeeklyScheduleID = $id;";
        cmd.Parameters.AddWithValue("$id", weeklyScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public List<(string employeeId, int dayWorkloadId)> GetEmployeeDayAssignments(int weeklyScheduleId)
    {
        var result = new List<(string, int)>();
        using var connection = OpenConnection();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT EmployeeID, DayWorkloadID
            FROM EmployeeDailySchedule
            WHERE WeeklyScheduleID = $id;";
        cmd.Parameters.AddWithValue("$id", weeklyScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt32(1)));

        return result;
    }

    // -------------------------------
    // DaySchedule operations (actual dates)
    // -------------------------------
    public int InsertDaySchedule(int weeklyScheduleId, DateTime scheduleDate, int? dayWorkloadId = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DaySchedule (WeeklyScheduleID, ScheduleDate, DayWorkloadID)
                VALUES ($scheduleId, $date, $workload);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$scheduleId", weeklyScheduleId);
            cmd.Parameters.AddWithValue("$date", scheduleDate);
            cmd.Parameters.AddWithValue("$workload", (object?)dayWorkloadId ?? DBNull.Value);
            long id = (long)cmd.ExecuteScalar();
            transaction.Commit();
            return (int)id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<(int id, int weeklyScheduleId, DateTime date, int? dayWorkloadId)> GetDaySchedules(int weeklyScheduleId)
    {
        var result = new List<(int, int, DateTime, int?)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT DayScheduleID, WeeklyScheduleID, ScheduleDate, DayWorkloadID
            FROM DaySchedule
            WHERE WeeklyScheduleID = $id
            ORDER BY ScheduleDate;";
        cmd.Parameters.AddWithValue("$id", weeklyScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)
            ));
        }
        return result;
    }

    public void DeleteDaySchedule(int dayScheduleId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM DaySchedule WHERE DayScheduleID = $id;";
            cmd.Parameters.AddWithValue("$id", dayScheduleId);
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public List<(string employeeId, string name)>
        GetEmployeesForSkill(int workLoadId)
    {
        var result = new List<(string, string)>();
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT e.EmployeeID, e.Name
        FROM Employee e
        JOIN EmployeeSkill es ON e.EmployeeID = es.EmployeeID
        WHERE es.WorkLoadID = $id;";
        cmd.Parameters.AddWithValue("$id", workLoadId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }

        return result;
    }
    public int GetEmployeeSkillCount(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT COUNT(*)
        FROM EmployeeSkill
        WHERE EmployeeID = $emp AND WorkLoadID = $wl;";
        cmd.Parameters.AddWithValue("$emp", employeeId);
        cmd.Parameters.AddWithValue("$wl", workLoadId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    /// <summary>
    /// Returns all DaySchedules linked to a specific WeeklySchedule.
    /// </summary>
    /// <param name="weeklyScheduleId">The ID of the WeeklySchedule.</param>
    /// <returns>List of (DayScheduleID, DayName) tuples.</returns>
    public List<(int dayScheduleId, string dayName)> GetDaySchedulesForWeeklySchedule(int weeklyScheduleId)
    {
        var result = new List<(int, string)>();

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT ds.DayScheduleID, dw.Day
        FROM DaySchedule ds
        LEFT JOIN DayWorkload dw ON ds.DayWorkloadID = dw.DayWorkloadID
        WHERE ds.WeeklyScheduleID = $sched;";
        cmd.Parameters.AddWithValue("$sched", weeklyScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            string day = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
            result.Add((id, day));
        }

        return result;
    }
    /// <summary>
    /// Returns all WorkLoads linked to a given DaySchedule.
    /// </summary>
    /// <param name="dayScheduleId">The DaySchedule ID.</param>
    /// <returns>List of (WorkLoadID, Name, Type) tuples.</returns>
    public List<(int workLoadId, string name, string type)> GetWorkLoadsForDaySchedule(int dayScheduleId)
    {
        var result = new List<(int, string, string)>();

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT w.WorkLoadID, w.Name, w.WorkLoadType
        FROM WorkLoad w
        JOIN DayWorkloadWorkLoad dww ON w.WorkLoadID = dww.WorkLoadID
        JOIN DaySchedule ds ON ds.DayWorkloadID = dww.DayWorkloadID
        WHERE ds.DayScheduleID = $ds;";
        cmd.Parameters.AddWithValue("$ds", dayScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return result;
    }
    /// <summary>
    /// Assigns an employee to a specific workload for a given DaySchedule.
    /// </summary>
    public void AssignEmployeeToWorkLoad(string employeeId, int workLoadId, int dayScheduleId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO EmployeeWorkLoadAssignment (EmployeeID, WorkLoadID, DayScheduleID)
            VALUES ($emp, $work, $day);";
            cmd.Parameters.AddWithValue("$emp", employeeId);
            cmd.Parameters.AddWithValue("$work", workLoadId);
            cmd.Parameters.AddWithValue("$day", dayScheduleId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    /// <summary>
    /// Returns all employee–workload assignments for a given WeeklySchedule.
    /// </summary>
    public List<(string employeeId, int workLoadId, int dayScheduleId)> GetAssignmentsForWeeklySchedule(int weeklyScheduleId)
    {
        var result = new List<(string, int, int)>();

        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
        SELECT ewa.EmployeeID, ewa.WorkLoadID, ewa.DayScheduleID
        FROM EmployeeWorkLoadAssignment ewa
        JOIN DaySchedule ds ON ewa.DayScheduleID = ds.DayScheduleID
        WHERE ds.WeeklyScheduleID = $sched;";
        cmd.Parameters.AddWithValue("$sched", weeklyScheduleId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        }

        return result;
    }
    
    /// <summary>
    /// Creates a WeeklyWorkload (template) and returns its ID.
    /// </summary>
    public int InsertWeeklyWorkload(string name, string description)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WeeklyWorkload (Name, Description)
            VALUES ($name, $desc);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$desc", description);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }



    /// <summary>
    /// Links a WorkLoad to a DayWorkload (populates DayWorkloadWorkLoad).
    /// </summary>
    public void AddWorkLoadToDayWorkload(int dayWorkloadId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DayWorkloadWorkLoad (DayWorkloadID, WorkLoadID)
            VALUES ($dayWl, $wl);";
        cmd.Parameters.AddWithValue("$dayWl", dayWorkloadId);
        cmd.Parameters.AddWithValue("$wl", workLoadId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Convenience: creates a WeeklyWorkload with 7 DayWorkloads (Mon..Sun).
    /// Returns WeeklyWorkloadID and fills DayWorkload rows (no tasks yet).
    /// </summary>
    public int InsertWeeklyWorkloadWithSevenDays(string name, string description = "")
    {
        var id = InsertWeeklyWorkload(name, description);
        // Adjust names to whatever you use in your app/tests
        var days = new[] { "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday" };
        foreach (var d in days)
            InsertDayWorkload(d, id);
        return id;
    }







}
