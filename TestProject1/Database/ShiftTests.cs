using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.TestProject1.Database;

[TestFixture]
public class ShiftTests : DatabaseTestBase
{
    private DatabaseHandler db = null!;

    [SetUp]
    public void LocalSetup()
    {
        db = DatabaseHandler.Instance;
    }

    [Test]
    public void InsertShift_CreatesShiftRecord()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0);
        var end = new DateTime(2025, 1, 1, 17, 0, 0);
        int shiftId = db.InsertShift(start, end);

        var shift = db.GetShiftById(shiftId);
        Assert.That(shift.HasValue, Is.True);
        Assert.That(shift?.start, Is.EqualTo(start));
        Assert.That(shift?.end, Is.EqualTo(end));
    }

    [Test]
    public void AddBreakToShift_AddsBreakCorrectly()
    {
        var start = DateTime.Today.AddHours(9);
        var end = DateTime.Today.AddHours(17);
        int shiftId = db.InsertShift(start, end);

        var breakTime = DateTime.Today.AddHours(12);
        db.AddBreakToShift(shiftId, breakTime);

        var shift = db.GetShiftById(shiftId);
        Assert.That(shift.HasValue, Is.True);
        Assert.That(shift?.breaks.Contains(breakTime), Is.True);
    }

    [Test]
    public void DeleteShift_RemovesShiftAndBreaks()
    {
        var start = DateTime.Today.AddHours(8);
        var end = DateTime.Today.AddHours(16);
        int shiftId = db.InsertShift(start, end);
        db.AddBreakToShift(shiftId, DateTime.Today.AddHours(11));

        db.DeleteShift(shiftId);

        var shift = db.GetShiftById(shiftId);
        Assert.That(shift.HasValue, Is.False, "Shift should be deleted.");
    }
}