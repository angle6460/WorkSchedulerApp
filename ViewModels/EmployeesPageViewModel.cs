using WorkSchedulerApp.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkSchedulerApp.Database;
using WorkSchedulerApp.Models;

namespace WorkSchedulerApp.ViewModels;

public partial class EmployeesPageViewModel : PageViewModel
{
    [ObservableProperty]
    private ObservableCollection<Employee> _employees = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoolSelectedEmployee))]
    [NotifyPropertyChangedFor(nameof(EditingEmployee))]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditingEmployee))]
    private Employee _newEmployee = new();

    private readonly DatabaseHandler db;

    public EmployeesPageViewModel()
    {
        PageName = ApplicationPageNames.Employees;
        db = DatabaseHandler.Instance;

        // fire-and-forget initial load (keeps constructor sync for XAML)
        _ = LoadEmployeesAsync();
    }

    // Always points to the object currently being edited
    public Employee EditingEmployee => SelectedEmployee ?? NewEmployee;

    public bool BoolSelectedEmployee => SelectedEmployee is not null;

    public bool CanDelete => SelectedEmployee is not null;

    // -------------------------------
    // CRUD Commands (async)
    // -------------------------------

    [RelayCommand]
    private async Task LoadEmployeesAsync()
    {
        Employees.Clear();

        var rows = await db.GetAllEmployeesAsync();
        Employees.Clear();

        foreach (var (id, name, role, requested, availability, contracted) in rows)
        {
            Employees.Add(new Employee
            {
                EmployeeId = id,
                Name = name,
                Role = role,
                RequestedHours = requested,
                Availability = availability,
                ContractedHours = contracted
            });
        }

    }

    [RelayCommand]
    private async Task AddEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEmployee.Name))
        {
            Console.WriteLine("Name is required.");
            return;
        }

        try
        {
            // EmployeeId should already be set (e.g., in model ctor as GUID).
            await db.InsertEmployeeAsync(
                NewEmployee.EmployeeId,
                NewEmployee.Name,
                NewEmployee.Role,
                NewEmployee.RequestedHours,
                NewEmployee.Availability,
                NewEmployee.ContractedHours
            );

            Employees.Add(NewEmployee);

            // Reset form with a new GUID
            NewEmployee = new Employee();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error adding employee: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UpdateEmployeeAsync()
    {
        if (SelectedEmployee is null) return;

        await db.UpdateEmployeeAsync(
            SelectedEmployee.EmployeeId,
            SelectedEmployee.Name,
            SelectedEmployee.Role,
            SelectedEmployee.RequestedHours,
            SelectedEmployee.Availability,
            SelectedEmployee.ContractedHours
        );

        // Optional: refresh list if your UI needs a clean reread
        await LoadEmployeesAsync();
        // Otherwise, the collection item is already updated via binding.
    }

    [RelayCommand]
    private async Task DeleteEmployeeAsync()
    {
        if (SelectedEmployee is null) return;

        await db.DeleteEmployeeAsync(SelectedEmployee.EmployeeId);
        Employees.Remove(SelectedEmployee);
        SelectedEmployee = null;
    }

    [RelayCommand]
    private void ClearFields()
    {
        SelectedEmployee = null;
        NewEmployee = new Employee();
    }
}
