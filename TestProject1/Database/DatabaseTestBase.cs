using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database
{
    /// <summary>
    /// Provides a base for all database-related integration tests.
    /// Automatically creates and initializes a fresh SQLite database
    /// for each test run using the new async DatabaseHandler.
    /// </summary>
    public abstract class DatabaseTestBase
    {
        protected static string TestDbPath = string.Empty;
        protected static bool Initialized = false;

        [OneTimeSetUp]
        public async Task GlobalSetupAsync()
        {
            if (Initialized && !string.IsNullOrEmpty(TestDbPath))
                return; // already set up once

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var testDbDir = Path.Combine(baseDir, "TestDB");
            Directory.CreateDirectory(testDbDir);

            TestDbPath = Path.Combine(testDbDir, $"TestDB_{Guid.NewGuid()}.db");
            Console.WriteLine($"[TEST DB PATH] {TestDbPath}");

            if (File.Exists(TestDbPath))
                File.Delete(TestDbPath);

            // Initialize the database asynchronously
            var db = await DatabaseHandler.Instance.InitializeAsync($"Data Source={TestDbPath};");

            // Just to verify schema creation
            Assert.That(File.Exists(TestDbPath), Is.True, "Database file should exist after schema creation.");

            Initialized = true;
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            DatabaseHandler.Instance.Close();
            Console.WriteLine("[TEST DB CLOSED]");
        }
    }
}