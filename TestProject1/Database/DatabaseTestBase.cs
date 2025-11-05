using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

public abstract class DatabaseTestBase
{
    protected static string TestDbPath = string.Empty;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        if (!string.IsNullOrEmpty(TestDbPath))
            return; // reuse existing DB

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var testDbDir = Path.Combine(baseDir, "TestDB");
        Directory.CreateDirectory(testDbDir);

        TestDbPath = Path.Combine(testDbDir, $"TestDB_{Guid.NewGuid()}.db");
        Console.WriteLine($"[TEST DB PATH] {TestDbPath}");

        if (File.Exists(TestDbPath))
            File.Delete(TestDbPath);

        DatabaseHandler.Instance.Initialize($"Data Source={TestDbPath};");
        Assert.That(File.Exists(TestDbPath), Is.True, "Database file should exist after schema creation.");
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        DatabaseHandler.Instance.Close();
    }
}