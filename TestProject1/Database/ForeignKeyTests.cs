using Microsoft.Data.Sqlite;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class ForeignKeyTests : DatabaseTestBase
{
    [Test]
    public void ForeignKeys_AreEnabled()
    {
        var db = DatabaseHandler.Instance;

        using var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        int result = Convert.ToInt32(cmd.ExecuteScalar());

        Assert.That(result, Is.EqualTo(1), "Foreign key enforcement should be ON.");
    }
}