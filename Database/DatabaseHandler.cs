using Microsoft.Data.Sqlite;

namespace WorkSchedulerApp.Database;

public sealed class DatabaseHandler
{
    private static DatabaseHandler? _instance;
    private static readonly Lock Lock = new();
    private string _connectionString = @"UNKNOWN";

    private DatabaseHandler()
    {
        // not needed here anymore 
        // EnsureSchema();
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
        ConnectionString  = connectionString; // Also EnsuresSchema
        return Instance;
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

    // Helper for connection reuse
    private SqliteConnection OpenConnection()
    {
        if (_connectionString == "UNKNOWN")
        {
            throw new InvalidOperationException("Cannot connect to SQL Server");
        }
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    // ---------------------------------------------------------------------
    // Schema Creation
    // ---------------------------------------------------------------------
    private void EnsureSchema()
    {
        using var connection = OpenConnection();

        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
-- ================================
-- WorkLoad Base Table (Polymorphic)
-- ================================
CREATE TABLE IF NOT EXISTS WorkLoad (
    WorkLoadID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    EstimatedHours INTEGER NOT NULL,
    WorkLoadType TEXT NOT NULL
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

-- ========== Employees ==========
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

-- ====== Shifts ======
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
-- =====================
-- Day Schedule (Actual daily plans under WeeklySchedule)
-- =====================
CREATE TABLE IF NOT EXISTS DaySchedule (
    DayScheduleID INTEGER PRIMARY KEY AUTOINCREMENT,
    WeeklyScheduleID INTEGER NOT NULL,
    ScheduleDate DATE NOT NULL,
    DayWorkloadID INTEGER,
    FOREIGN KEY (WeeklyScheduleID) REFERENCES WeeklySchedule(WeeklyScheduleID) ON DELETE CASCADE,
    FOREIGN KEY (DayWorkloadID) REFERENCES DayWorkload(DayWorkloadID)
);
";
        createTableCommand.ExecuteNonQuery();
    }

    public void Close()
    {
        SqliteConnection.ClearAllPools();
    }

    // ---------------------------------------------------------------------
    // Insert Methods
    // ---------------------------------------------------------------------
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
    
    public string InsertEmployee(string employeeId, string name, string role, int requestedHours, string availability, string contractedHours)
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

        return employeeId;
    }


    // ---------------------------------------------------------------------
    // View / Query Commands
    // ---------------------------------------------------------------------
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
        {
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        }
        return result;
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
    
    // ---------------------------------------------------------------------
    // Employee Skill Mapping (Many-to-Many Link Table)
    // ---------------------------------------------------------------------

    public void AddSkillToEmployee(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO EmployeeSkills (EmployeeID, WorkLoadID)
                VALUES ($empId, $workLoadId);";
            cmd.Parameters.AddWithValue("$empId", employeeId);
            cmd.Parameters.AddWithValue("$workLoadId", workLoadId);
            cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void RemoveSkillFromEmployee(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM EmployeeSkills
            WHERE EmployeeID = $empId AND WorkLoadID = $workLoadId;";
        cmd.Parameters.AddWithValue("$empId", employeeId);
        cmd.Parameters.AddWithValue("$workLoadId", workLoadId);
        cmd.ExecuteNonQuery();
    }

    public List<(int workLoadId, string workLoadName)> GetSkillsForEmployee(string employeeId)
    {
        var result = new List<(int, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT wl.WorkLoadID, wl.Name
            FROM WorkLoad wl
            JOIN EmployeeSkills es ON wl.WorkLoadID = es.WorkLoadID
            WHERE es.EmployeeID = $id;";
        cmd.Parameters.AddWithValue("$id", employeeId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        return result;
    }

    public List<(string employeeId, string employeeName)> GetEmployeesForSkill(int workLoadId)
    {
        var result = new List<(string, string)>();
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT e.EmployeeID, e.Name
            FROM Employee e
            JOIN EmployeeSkills es ON e.EmployeeID = es.EmployeeID
            WHERE es.WorkLoadID = $id;";
        cmd.Parameters.AddWithValue("$id", workLoadId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }

        return result;
    }
    // ---------------------------------------------------------------------
    // Utility / Helper Queries
    // ---------------------------------------------------------------------

    public int GetWorkLoadIdByName(string name)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkLoadID FROM WorkLoad WHERE Name=$name;";
        cmd.Parameters.AddWithValue("$name", name);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : -1;
    }

    public int GetEmployeeSkillCount(string employeeId, int workLoadId)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM EmployeeSkills 
            WHERE EmployeeID=$emp AND WorkLoadID=$wl;";
        cmd.Parameters.AddWithValue("$emp", employeeId);
        cmd.Parameters.AddWithValue("$wl", workLoadId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetWorkLoadCountByName(string name)
    {
        using var connection = OpenConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WorkLoad WHERE Name=$name;";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
    // ---------------------------------------------------------------------
    // Delete Operations
    // ---------------------------------------------------------------------

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
    // ---------------------------------------------------------------------
    // Update Operations
    // ---------------------------------------------------------------------

    public void UpdateEmployee(
        string employeeId,
        string name,
        string role,
        int requestedHours,
        string availability,
        string contractedHours)
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

    public void UpdateWorkLoad(
        int workLoadId,
        string name,
        string description,
        int estimatedHours)
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
    // ---------------------------------------------------------------------
    // WorkGroup Update / Delete Operations
    // ---------------------------------------------------------------------

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
    // ---------------------------------------------------------------------
    // Shift and Break Operations
    // ---------------------------------------------------------------------

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

        // Main shift record
        var shiftCmd = connection.CreateCommand();
        shiftCmd.CommandText = "SELECT ShiftID, StartTime, EndTime FROM Shift WHERE ShiftID = $id;";
        shiftCmd.Parameters.AddWithValue("$id", shiftId);

        using var reader = shiftCmd.ExecuteReader();
        if (!reader.Read()) return null;

        var id = reader.GetInt32(0);
        var start = reader.GetDateTime(1);
        var end = reader.GetDateTime(2);

        // Get breaks
        var breaks = new List<DateTime>();
        var breakCmd = connection.CreateCommand();
        breakCmd.CommandText = "SELECT BreakTime FROM ShiftBreak WHERE ShiftID = $id;";
        breakCmd.Parameters.AddWithValue("$id", shiftId);
        using var breakReader = breakCmd.ExecuteReader();
        while (breakReader.Read())
        {
            breaks.Add(breakReader.GetDateTime(0));
        }

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
        {
            result.Add((reader.GetInt32(0), reader.GetDateTime(1), reader.GetDateTime(2)));
        }

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
    // ---------------------------------------------------------------------
    // Weekly Schedule Management
    // ---------------------------------------------------------------------

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
        {
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        return result;
    }
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
    // ---------------------------------------------------------------------
    // Weekly Schedule Cloning (from WeeklyWorkload Template + DaySchedule auto-generation)
    // ---------------------------------------------------------------------
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

            // STEP 2: Get all DayWorkloads for this WeeklyWorkload
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

            // STEP 3: Create cloned DayWorkloads linked to the template (deep copy)
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

            // STEP 4: Copy DayWorkload → WorkLoad mappings
            foreach (var pair in newDayMap)
            {
                var mapCmd = connection.CreateCommand();
                mapCmd.CommandText = @"
                    INSERT INTO DayWorkloadWorkLoad (DayWorkloadID, WorkLoadID)
                    SELECT $newId, WorkLoadID
                    FROM DayWorkloadWorkLoad
                    WHERE DayWorkloadID = $oldId;";
                mapCmd.Parameters.AddWithValue("$newId", pair.Value);
                mapCmd.Parameters.AddWithValue("$oldId", pair.Key);
                mapCmd.ExecuteNonQuery();
            }

            // STEP 5: Auto-generate 7 DaySchedule entries (Mon–Sun)
            for (int i = 0; i < 7; i++)
            {
                DateTime date = weekStart.AddDays(i);

                // Try to match a DayWorkload by name (e.g., "Monday", "Tuesday")
                string weekdayName = date.DayOfWeek.ToString();
                int? matchingWorkloadId = newDayMap
                    .Where(d => d.Value > 0 && 
                                daysToClone.Any(o => o.oldId == d.Key && 
                                string.Equals(o.dayName, weekdayName, StringComparison.OrdinalIgnoreCase)))
                    .Select(d => (int?)d.Value)
                    .FirstOrDefault();

                var insertScheduleDay = connection.CreateCommand();
                insertScheduleDay.CommandText = @"
                    INSERT INTO DaySchedule (WeeklyScheduleID, ScheduleDate, DayWorkloadID)
                    VALUES ($schedule, $date, $workload);";
                insertScheduleDay.Parameters.AddWithValue("$schedule", newScheduleId);
                insertScheduleDay.Parameters.AddWithValue("$date", date);
                insertScheduleDay.Parameters.AddWithValue("$workload", (object?)matchingWorkloadId ?? DBNull.Value);
                insertScheduleDay.ExecuteNonQuery();
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
    // ---------------------------------------------------------------------
    // DaySchedule Operations
    // ---------------------------------------------------------------------

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

}
