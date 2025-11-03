using WorkScedulerApp.Models;
using WorkScedulerApp.Database;
namespace TestProject1;

public class Tests
{
    DatabaseHandler dbHandler = DatabaseHandler.Instance;
    
    [SetUp]
    public void Setup()
    {
        // dbHandler.ConnectionString = "Data Source=../path/to/WorkScedulerApp/bin/Debug/net9.0/Database.db";
    }

    [Test]
    public void Test1()
    {
        Console.WriteLine(dbHandler.ConnectionString);
        Assert.That(dbHandler.ConnectionString, Is.EqualTo(@"Data Source=C:\Users\Angel\RiderProjects\WorkScedulerApp\Database\Database.db;"));
    }
}