using Microsoft.Data.Sqlite;

namespace WorkScedulerApp.Database;

public sealed class DatabaseHandler
{
    private static DatabaseHandler? _instance;
    private static readonly object Lock = new();
    private string _connectionString = @"Data Source=C:\Users\Angel\RiderProjects\WorkScedulerApp\Database\Database.db;";

    private DatabaseHandler()
    {
        EnsureSchema(); // ensure schema for initial connection
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

    // Automatically rebuild schema when connection string changes
    public string ConnectionString
    {
        get => _connectionString;
        set
        {
            _connectionString = value;
            EnsureSchema();
        }
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

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
";
        createTableCommand.ExecuteNonQuery();
    }
    public void Close()
    {
        // Force release of file handles
        SqliteConnection.ClearAllPools();
    }

    // -------------------------------
    // Insert Methods
    // -------------------------------

    public void InsertPerEmployeeWorkLoad(string name, string description, int minutesPerEmployee, int numberOfEmployees)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void InsertPerItemWorkLoad(string name, string description, int minutesPerItem, int numberOfItems)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void InsertFixedWorkLoad(string name, string description, int fixedHours)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void InsertWorkGroup(string name, List<int> workLoadIds)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    // -------------------------------
// View / Query Commands
// -------------------------------

public List<(int id, string name, string type)> GetAllWorkLoads()
{
    var result = new List<(int, string, string)>();
    using var connection = new SqliteConnection(_connectionString);
    connection.Open();

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
    using var connection = new SqliteConnection(_connectionString);
    connection.Open();

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
    using var connection = new SqliteConnection(_connectionString);
    connection.Open();

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
    using var connection = new SqliteConnection(_connectionString);
    connection.Open();

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

}
