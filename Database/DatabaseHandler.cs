using Microsoft.Data.Sqlite;

namespace WorkSchedulerApp.Database;

public sealed class DatabaseHandler
{
    private static DatabaseHandler? _instance;
    private static readonly object Lock = new();
    private string _connectionString = "UNKNOWN";

    private DatabaseHandler() { }

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

    public async Task<DatabaseHandler> InitializeAsync(string connectionString)
    {
        _connectionString = connectionString;
        await EnsureSchemaAsync();
        return this;
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        if (_connectionString == "UNKNOWN")
            throw new InvalidOperationException("Connection string not set.");

        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        await pragma.ExecuteNonQueryAsync();

        return conn;
    }

    private async Task EnsureSchemaAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
CREATE TABLE IF NOT EXISTS Employee (
    EmployeeID TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Role TEXT,
    RequestedHours INTEGER,
    Availability TEXT,
    ContractedHours TEXT
);

CREATE TABLE IF NOT EXISTS WorkLoadTemplate (
    WorkLoadTemplateID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    EstimatedHours REAL NOT NULL,
    WorkLoadType TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PerEmployeeWorkLoadTemplate (
    WorkLoadTemplateID INTEGER PRIMARY KEY,
    MinutesPerEmployee INTEGER NOT NULL,
    NumberOfEmployees INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS PerItemWorkLoadTemplate (
    WorkLoadTemplateID INTEGER PRIMARY KEY,
    MinutesPerItem INTEGER NOT NULL,
    NumberOfItems INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS FixedWorkLoadTemplate (
    WorkLoadTemplateID INTEGER PRIMARY KEY,
    FixedHours INTEGER NOT NULL,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS EmployeeSkill (
    EmployeeID TEXT NOT NULL,
    WorkLoadTemplateID INTEGER NOT NULL,
    PRIMARY KEY (EmployeeID, WorkLoadTemplateID),
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS WorkGroup (
    WorkGroupID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS WorkGroupWorkLoadTemplate (
    WorkGroupID INTEGER NOT NULL,
    WorkLoadTemplateID INTEGER NOT NULL,
    PRIMARY KEY (WorkGroupID, WorkLoadTemplateID),
    FOREIGN KEY (WorkGroupID) REFERENCES WorkGroup(WorkGroupID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS WeeklyWorkloadTemplate (
    WeeklyWorkloadTemplateID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT
);

CREATE TABLE IF NOT EXISTS DayWorkloadTemplate (
    DayWorkloadTemplateID INTEGER PRIMARY KEY AUTOINCREMENT,
    Day TEXT NOT NULL,
    WeeklyWorkloadTemplateID INTEGER NOT NULL,
    FOREIGN KEY (WeeklyWorkloadTemplateID) REFERENCES WeeklyWorkloadTemplate(WeeklyWorkloadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS DayWorkloadTemplateWorkLoadTemplate (
    DayWorkloadTemplateID INTEGER NOT NULL,
    WorkLoadTemplateID INTEGER NOT NULL,
    PRIMARY KEY (DayWorkloadTemplateID, WorkLoadTemplateID),
    FOREIGN KEY (DayWorkloadTemplateID) REFERENCES DayWorkloadTemplate(DayWorkloadTemplateID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS WeeklyWorkloadInstance (
    WeeklyWorkloadInstanceID INTEGER PRIMARY KEY AUTOINCREMENT,
    WeekStart DATE NOT NULL,
    WeekEnd DATE NOT NULL,
    WeeklyWorkloadTemplateID INTEGER NOT NULL,
    FOREIGN KEY (WeeklyWorkloadTemplateID) REFERENCES WeeklyWorkloadTemplate(WeeklyWorkloadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS DayWorkloadInstance (
    DayWorkloadInstanceID INTEGER PRIMARY KEY AUTOINCREMENT,
    Day TEXT NOT NULL,
    WeeklyWorkloadInstanceID INTEGER NOT NULL,
    DayWorkloadTemplateID INTEGER NOT NULL,
    FOREIGN KEY (WeeklyWorkloadInstanceID) REFERENCES WeeklyWorkloadInstance(WeeklyWorkloadInstanceID) ON DELETE CASCADE,
    FOREIGN KEY (DayWorkloadTemplateID) REFERENCES DayWorkloadTemplate(DayWorkloadTemplateID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS WorkLoadInstance (
    WorkLoadInstanceID INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkLoadTemplateID INTEGER NOT NULL,
    DayWorkloadInstanceID INTEGER NOT NULL,
    WeeklyWorkloadInstanceID INTEGER NOT NULL,
    EstimatedHours REAL,
    FOREIGN KEY (WorkLoadTemplateID) REFERENCES WorkLoadTemplate(WorkLoadTemplateID) ON DELETE CASCADE,
    FOREIGN KEY (DayWorkloadInstanceID) REFERENCES DayWorkloadInstance(DayWorkloadInstanceID) ON DELETE CASCADE,
    FOREIGN KEY (WeeklyWorkloadInstanceID) REFERENCES WeeklyWorkloadInstance(WeeklyWorkloadInstanceID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS EmployeeWorkLoadInstanceAssignment (
    AssignmentID INTEGER PRIMARY KEY AUTOINCREMENT,
    EmployeeID TEXT NOT NULL,
    WorkLoadInstanceID INTEGER NOT NULL,
    UNIQUE (EmployeeID, WorkLoadInstanceID),
    FOREIGN KEY (EmployeeID) REFERENCES Employee(EmployeeID) ON DELETE CASCADE,
    FOREIGN KEY (WorkLoadInstanceID) REFERENCES WorkLoadInstance(WorkLoadInstanceID) ON DELETE CASCADE
);
""";
        await cmd.ExecuteNonQueryAsync();
    }

    public void Close() => SqliteConnection.ClearAllPools();
    // === EMPLOYEES =============================================================
    public async Task InsertEmployeeAsync(string employeeId, string name, string role, int requestedHours, string availability, string contractedHours)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Employee (EmployeeID, Name, Role, RequestedHours, Availability, ContractedHours)
            VALUES (@id, @name, @role, @requested, @availability, @contracted);
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", employeeId),
            new SqliteParameter("@name", name),
            new SqliteParameter("@role", role),
            new SqliteParameter("@requested", requestedHours),
            new SqliteParameter("@availability", availability),
            new SqliteParameter("@contracted", contractedHours)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateEmployeeAsync(string employeeId, string name, string role, int requestedHours, string availability, string contractedHours)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Employee
            SET Name=@name, Role=@role, RequestedHours=@requested, Availability=@availability, ContractedHours=@contracted
            WHERE EmployeeID=@id;
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", employeeId),
            new SqliteParameter("@name", name),
            new SqliteParameter("@role", role),
            new SqliteParameter("@requested", requestedHours),
            new SqliteParameter("@availability", availability),
            new SqliteParameter("@contracted", contractedHours)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteEmployeeAsync(string employeeId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Employee WHERE EmployeeID=@id;";
        cmd.Parameters.Add(new SqliteParameter("@id", employeeId));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string id, string name, string role)>> GetAllEmployeesAsync()
    {
        var result = new List<(string, string, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmployeeID, Name, Role FROM Employee;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
        return result;
    }

    // === WORKLOAD TEMPLATES ====================================================
    public async Task<int> InsertPerEmployeeWorkLoadTemplateAsync(string name, string description, int minutesPerEmployee, int numberOfEmployees)
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var baseCmd = connection.CreateCommand();
            baseCmd.CommandText = """
                INSERT INTO WorkLoadTemplate (Name, Description, EstimatedHours, WorkLoadType)
                VALUES (@n, @d, @h, 'PerEmployee');
                SELECT last_insert_rowid();
            """;
            baseCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@n", name),
                new SqliteParameter("@d", description),
                new SqliteParameter("@h", (minutesPerEmployee * numberOfEmployees) / 60.0)
            });
            var id = Convert.ToInt32(await baseCmd.ExecuteScalarAsync());

            var detailCmd = connection.CreateCommand();
            detailCmd.CommandText = """
                INSERT INTO PerEmployeeWorkLoadTemplate (WorkLoadTemplateID, MinutesPerEmployee, NumberOfEmployees)
                VALUES (@id, @m, @n);
            """;
            detailCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@id", id),
                new SqliteParameter("@m", minutesPerEmployee),
                new SqliteParameter("@n", numberOfEmployees)
            });
            await detailCmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> InsertPerItemWorkLoadTemplateAsync(string name, string description, int minutesPerItem, int numberOfItems)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var baseCmd = connection.CreateCommand();
            baseCmd.CommandText = """
                INSERT INTO WorkLoadTemplate (Name, Description, EstimatedHours, WorkLoadType)
                VALUES (@n, @d, @h, 'PerItem');
                SELECT last_insert_rowid();
            """;
            baseCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@n", name),
                new SqliteParameter("@d", description),
                new SqliteParameter("@h", (minutesPerItem * numberOfItems) / 60.0)
            });
            var id = Convert.ToInt32(await baseCmd.ExecuteScalarAsync());

            var detailCmd = connection.CreateCommand();
            detailCmd.CommandText = """
                INSERT INTO PerItemWorkLoadTemplate (WorkLoadTemplateID, MinutesPerItem, NumberOfItems)
                VALUES (@id, @m, @n);
            """;
            detailCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@id", id),
                new SqliteParameter("@m", minutesPerItem),
                new SqliteParameter("@n", numberOfItems)
            });
            await detailCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<int> InsertFixedWorkLoadTemplateAsync(string name, string description, double fixedHours)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var baseCmd = connection.CreateCommand();
            baseCmd.CommandText = """
                INSERT INTO WorkLoadTemplate (Name, Description, EstimatedHours, WorkLoadType)
                VALUES (@n, @d, @h, 'Fixed');
                SELECT last_insert_rowid();
            """;
            baseCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@n", name),
                new SqliteParameter("@d", description),
                new SqliteParameter("@h", fixedHours)
            });
            var id = Convert.ToInt32(await baseCmd.ExecuteScalarAsync());

            var fixCmd = connection.CreateCommand();
            fixCmd.CommandText = """
                INSERT INTO FixedWorkLoadTemplate (WorkLoadTemplateID, FixedHours)
                VALUES (@id, @h);
            """;
            fixCmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@id", id),
                new SqliteParameter("@h", fixedHours)
            });
            await fixCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // === WORKGROUPS ============================================================
    public async Task<int> InsertWorkGroupWithTemplatesAsync(string name, List<int> workLoadTemplateIds)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var insertGroup = connection.CreateCommand();
            insertGroup.CommandText = """
                INSERT INTO WorkGroup (Name) VALUES (@n);
                SELECT last_insert_rowid();
            """;
            insertGroup.Parameters.Add(new SqliteParameter("@n", name));
            var workGroupId = Convert.ToInt32(await insertGroup.ExecuteScalarAsync());

            foreach (var id in workLoadTemplateIds)
            {
                var mapCmd = connection.CreateCommand();
                mapCmd.CommandText = """
                    INSERT INTO WorkGroupWorkLoadTemplate (WorkGroupID, WorkLoadTemplateID)
                    VALUES (@g, @w);
                """;
                mapCmd.Parameters.AddRange(new[]
                {
                    new SqliteParameter("@g", workGroupId),
                    new SqliteParameter("@w", id)
                });
                await mapCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return workGroupId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateWorkGroupAsync(int workGroupId, string newName)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE WorkGroup SET Name=@n WHERE WorkGroupID=@id;";
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", workGroupId),
            new SqliteParameter("@n", newName)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteWorkGroupAsync(int workGroupId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WorkGroup WHERE WorkGroupID=@id;";
        cmd.Parameters.Add(new SqliteParameter("@id", workGroupId));
        await cmd.ExecuteNonQueryAsync();
    }

    // === EMPLOYEE SKILLS (WorkLoadTemplate links) ==============================
    public async Task AddTemplateSkillToEmployeeAsync(string employeeId, int workLoadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO EmployeeSkill (EmployeeID, WorkLoadTemplateID)
            VALUES (@e, @w);
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@e", employeeId),
            new SqliteParameter("@w", workLoadTemplateId)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveTemplateSkillFromEmployeeAsync(string employeeId, int workLoadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM EmployeeSkill WHERE EmployeeID=@e AND WorkLoadTemplateID=@w;";
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@e", employeeId),
            new SqliteParameter("@w", workLoadTemplateId)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string employeeId, int workLoadTemplateId)>> GetAllEmployeeTemplateSkillsAsync()
    {
        var list = new List<(string, int)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EmployeeID, WorkLoadTemplateID FROM EmployeeSkill;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetString(0), reader.GetInt32(1)));
        return list;
    }
    // === WEEKLY & DAY WORKLOAD TEMPLATES ======================================
    public async Task<int> InsertWeeklyWorkloadTemplateAsync(string name, string description)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO WeeklyWorkloadTemplate (Name, Description)
            VALUES (@n, @d);
            SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@n", name),
            new SqliteParameter("@d", description)
        });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> InsertDayWorkloadTemplateAsync(string day, int weeklyWorkloadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO DayWorkloadTemplate (Day, WeeklyWorkloadTemplateID)
            VALUES (@day, @wk);
            SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@day", day),
            new SqliteParameter("@wk", weeklyWorkloadTemplateId)
        });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task AddWorkLoadTemplateToDayAsync(int dayWorkloadTemplateId, int workLoadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO DayWorkloadTemplateWorkLoadTemplate
            (DayWorkloadTemplateID, WorkLoadTemplateID)
            VALUES (@d, @w);
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@d", dayWorkloadTemplateId),
            new SqliteParameter("@w", workLoadTemplateId)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertWeeklyWorkloadTemplateWithSevenDaysAsync(string name, string description = "")
    {
        var id = await InsertWeeklyWorkloadTemplateAsync(name, description);
        var days = new[] { "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday" };
        foreach (var d in days)
            await InsertDayWorkloadTemplateAsync(d, id);
        return id;
    }

    // === INSTANCE LAYER =======================================================
    public async Task<int> CloneWeeklyWorkloadTemplateToInstanceAsync(int weeklyWorkloadTemplateId, DateTime weekStart, DateTime weekEnd)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var insWeekly = connection.CreateCommand();
            insWeekly.CommandText = """
                INSERT INTO WeeklyWorkloadInstance (WeekStart, WeekEnd, WeeklyWorkloadTemplateID)
                VALUES (@s, @e, @wk);
                SELECT last_insert_rowid();
            """;
            insWeekly.Parameters.AddRange(new[]
            {
                new SqliteParameter("@s", weekStart),
                new SqliteParameter("@e", weekEnd),
                new SqliteParameter("@wk", weeklyWorkloadTemplateId)
            });
            var weeklyInstanceId = Convert.ToInt32(await insWeekly.ExecuteScalarAsync());

            var getDays = connection.CreateCommand();
            getDays.CommandText = """
                SELECT DayWorkloadTemplateID, Day
                FROM DayWorkloadTemplate
                WHERE WeeklyWorkloadTemplateID=@wk
                ORDER BY DayWorkloadTemplateID;
            """;
            getDays.Parameters.Add(new SqliteParameter("@wk", weeklyWorkloadTemplateId));

            var dayTemplateMap = new List<(int templateId, string day)>();
            await using (var reader = await getDays.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    dayTemplateMap.Add((reader.GetInt32(0), reader.GetString(1)));
            }

            var map = new Dictionary<int, int>();
            foreach (var (tplId, dayName) in dayTemplateMap)
            {
                var insDay = connection.CreateCommand();
                insDay.CommandText = """
                    INSERT INTO DayWorkloadInstance (Day, WeeklyWorkloadInstanceID, DayWorkloadTemplateID)
                    VALUES (@d, @wkI, @tpl);
                    SELECT last_insert_rowid();
                """;
                insDay.Parameters.AddRange(new[]
                {
                    new SqliteParameter("@d", dayName),
                    new SqliteParameter("@wkI", weeklyInstanceId),
                    new SqliteParameter("@tpl", tplId)
                });
                var instanceId = Convert.ToInt32(await insDay.ExecuteScalarAsync());
                map[tplId] = instanceId;
            }

            foreach (var (tplId, _) in dayTemplateMap)
            {
                var fetchCmd = connection.CreateCommand();
                fetchCmd.CommandText = """
                    SELECT WorkLoadTemplateID
                    FROM DayWorkloadTemplateWorkLoadTemplate
                    WHERE DayWorkloadTemplateID=@d;
                """;
                fetchCmd.Parameters.Add(new SqliteParameter("@d", tplId));

                await using var rr = await fetchCmd.ExecuteReaderAsync();
                while (await rr.ReadAsync())
                {
                    var templateId = rr.GetInt32(0);
                    var insWork = connection.CreateCommand();
                    insWork.CommandText = """
                        INSERT INTO WorkLoadInstance
                        (WorkLoadTemplateID, DayWorkloadInstanceID, WeeklyWorkloadInstanceID)
                        VALUES (@tpl, @dwi, @wkI);
                    """;
                    insWork.Parameters.AddRange(new[]
                    {
                        new SqliteParameter("@tpl", templateId),
                        new SqliteParameter("@dwi", map[tplId]),
                        new SqliteParameter("@wkI", weeklyInstanceId)
                    });
                    await insWork.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            return weeklyInstanceId;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<(int dayWorkloadInstanceId, string day)>> GetDayWorkloadInstancesForWeeklyInstanceAsync(int weeklyInstanceId)
    {
        var list = new List<(int, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DayWorkloadInstanceID, Day
            FROM DayWorkloadInstance
            WHERE WeeklyWorkloadInstanceID=@id
            ORDER BY DayWorkloadInstanceID;
        """;
        cmd.Parameters.Add(new SqliteParameter("@id", weeklyInstanceId));
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetString(1)));
        return list;
    }

    public async Task<List<(int workLoadInstanceId, int workLoadTemplateId)>> GetWorkLoadInstancesForDayInstanceAsync(int dayWorkloadInstanceId)
    {
        var list = new List<(int, int)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT WorkLoadInstanceID, WorkLoadTemplateID
            FROM WorkLoadInstance
            WHERE DayWorkloadInstanceID=@d;
        """;
        cmd.Parameters.Add(new SqliteParameter("@d", dayWorkloadInstanceId));
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetInt32(1)));
        return list;
    }

    public async Task AssignEmployeeToWorkLoadInstanceAsync(string employeeId, int workLoadInstanceId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO EmployeeWorkLoadInstanceAssignment (EmployeeID, WorkLoadInstanceID)
                VALUES (@e, @w);
            """;
            cmd.Parameters.AddRange(new[]
            {
                new SqliteParameter("@e", employeeId),
                new SqliteParameter("@w", workLoadInstanceId)
            });
            await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(int workLoadInstanceId, string workLoadTemplateName)?> GetWorkLoadInstanceByIdAsync(int id)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT wli.WorkLoadInstanceID, wlt.Name
            FROM WorkLoadInstance wli
            JOIN WorkLoadTemplate wlt ON wli.WorkLoadTemplateID = wlt.WorkLoadTemplateID
            WHERE wli.WorkLoadInstanceID=@id;
        """;
        cmd.Parameters.Add(new SqliteParameter("@id", id));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return (reader.GetInt32(0), reader.GetString(1));
        return null;
    }
    // === QUERY HELPERS =========================================================
    public async Task<List<(int id, string name, string type)>> GetAllWorkLoadTemplatesAsync()
    {
        var list = new List<(int, string, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkLoadTemplateID, Name, WorkLoadType FROM WorkLoadTemplate;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return list;
    }

    public async Task<(string name, string description, double estimatedHours, string type)?> GetWorkLoadTemplateByIdAsync(int id)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Name, Description, EstimatedHours, WorkLoadType
            FROM WorkLoadTemplate
            WHERE WorkLoadTemplateID=@id;
        """;
        cmd.Parameters.Add(new SqliteParameter("@id", id));
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return (reader.GetString(0), reader.IsDBNull(1) ? "" : reader.GetString(1), reader.GetDouble(2), reader.GetString(3));
        return null;
    }

    public async Task<int> GetWorkLoadTemplateIdByNameAsync(string name)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkLoadTemplateID FROM WorkLoadTemplate WHERE Name=@n;";
        cmd.Parameters.Add(new SqliteParameter("@n", name));
        var result = await cmd.ExecuteScalarAsync();
        return result is not null ? Convert.ToInt32(result) : -1;
    }

    public async Task<int> GetWorkLoadTemplateCountByNameAsync(string name)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WorkLoadTemplate WHERE Name=@n;";
        cmd.Parameters.Add(new SqliteParameter("@n", name));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<(int id, string name)>> GetAllWorkGroupsAsync()
    {
        var list = new List<(int, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WorkGroupID, Name FROM WorkGroup;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public async Task<List<(int workLoadTemplateId, string name)>> GetWorkLoadTemplatesForGroupAsync(int workGroupId)
    {
        var list = new List<(int, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT w.WorkLoadTemplateID, w.Name
            FROM WorkLoadTemplate w
            JOIN WorkGroupWorkLoadTemplate gw ON gw.WorkLoadTemplateID = w.WorkLoadTemplateID
            WHERE gw.WorkGroupID=@id;
        """;
        cmd.Parameters.Add(new SqliteParameter("@id", workGroupId));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public async Task<List<(string employeeId, string name)>> GetEmployeesForTemplateSkillAsync(int workLoadTemplateId)
    {
        var list = new List<(string, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT e.EmployeeID, e.Name
            FROM Employee e
            JOIN EmployeeSkill es ON e.EmployeeID = es.EmployeeID
            WHERE es.WorkLoadTemplateID=@id;
        """;
        cmd.Parameters.Add(new SqliteParameter("@id", workLoadTemplateId));
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetString(0), reader.GetString(1)));
        return list;
    }

    public async Task<int> GetEmployeeTemplateSkillCountAsync(string employeeId, int workLoadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM EmployeeSkill
            WHERE EmployeeID=@e AND WorkLoadTemplateID=@w;
        """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@e", employeeId),
            new SqliteParameter("@w", workLoadTemplateId)
        });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // === UTILITY ===============================================================
    public static DateTime ResolveDateForDayName(DateTime weekStart, string dayName)
    {
        var dn = (dayName ?? "").Trim().ToLowerInvariant();
        var offset = dn switch
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
        return weekStart.Date.AddDays(offset);
    }
    // === WORKLOAD TEMPLATE UPDATES & DELETION ====================================
    public async Task UpdateWorkLoadTemplateAsync(int workLoadTemplateId, string name, string description, double estimatedHours)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                              UPDATE WorkLoadTemplate
                              SET Name=@n, Description=@d, EstimatedHours=@h
                              WHERE WorkLoadTemplateID=@id;
                          """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", workLoadTemplateId),
            new SqliteParameter("@n", name),
            new SqliteParameter("@d", description),
            new SqliteParameter("@h", estimatedHours)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteWorkLoadTemplateAsync(int workLoadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WorkLoadTemplate WHERE WorkLoadTemplateID=@id;";
        cmd.Parameters.Add(new SqliteParameter("@id", workLoadTemplateId));
        await cmd.ExecuteNonQueryAsync();
    }
    // === WEEKLY WORKLOAD TEMPLATE CRUD ===========================================
    public async Task<List<(int id, string name, string description)>> GetAllWeeklyWorkloadTemplatesAsync()
    {
        var list = new List<(int, string, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT WeeklyWorkloadTemplateID, Name, Description FROM WeeklyWorkloadTemplate;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
        return list;
    }

    public async Task UpdateWeeklyWorkloadTemplateAsync(int weeklyWorkloadTemplateId, string name, string description)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                              UPDATE WeeklyWorkloadTemplate
                              SET Name=@n, Description=@d
                              WHERE WeeklyWorkloadTemplateID=@id;
                          """;
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", weeklyWorkloadTemplateId),
            new SqliteParameter("@n", name),
            new SqliteParameter("@d", description)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteWeeklyWorkloadTemplateAsync(int weeklyWorkloadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WeeklyWorkloadTemplate WHERE WeeklyWorkloadTemplateID=@id;";
        cmd.Parameters.Add(new SqliteParameter("@id", weeklyWorkloadTemplateId));
        await cmd.ExecuteNonQueryAsync();
    }
    // === DAY WORKLOAD TEMPLATE CRUD =============================================
    public async Task<List<(int id, string day, int weeklyWorkloadTemplateId)>> GetAllDayWorkloadTemplatesAsync()
    {
        var list = new List<(int, string, int)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DayWorkloadTemplateID, Day, WeeklyWorkloadTemplateID FROM DayWorkloadTemplate;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
        return list;
    }

    public async Task UpdateDayWorkloadTemplateAsync(int dayWorkloadTemplateId, string newDay)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE DayWorkloadTemplate SET Day=@d WHERE DayWorkloadTemplateID=@id;";
        cmd.Parameters.AddRange(new[]
        {
            new SqliteParameter("@id", dayWorkloadTemplateId),
            new SqliteParameter("@d", newDay)
        });
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteDayWorkloadTemplateAsync(int dayWorkloadTemplateId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DayWorkloadTemplate WHERE DayWorkloadTemplateID=@id;";
        cmd.Parameters.Add(new SqliteParameter("@id", dayWorkloadTemplateId));
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<List<(int id, string day)>> GetDayWorkloadTemplatesForWeeklyTemplateAsync(int weeklyWorkloadTemplateId)
    {
        var list = new List<(int, string)>();
        await using var connection = await OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                              SELECT DayWorkloadTemplateID, Day
                              FROM DayWorkloadTemplate
                              WHERE WeeklyWorkloadTemplateID=@wk
                              ORDER BY DayWorkloadTemplateID;
                          """;
        cmd.Parameters.Add(new SqliteParameter("@wk", weeklyWorkloadTemplateId));
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetString(1)));
        return list;
    }






}
