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
        LoadEmployees();
    }

    // Always points to the object currently being edited
    public Employee EditingEmployee => SelectedEmployee ?? NewEmployee;

    public bool BoolSelectedEmployee => SelectedEmployee is not null;
    
    public bool CanDelete => SelectedEmployee is not null;

    // -------------------------------
    // CRUD Commands
    // -------------------------------

    [RelayCommand]
    private void LoadEmployees()
    {
        Employees.Clear();
        foreach (var (id, name, role) in db.GetAllEmployees())
        {
            Employees.Add(new Employee
            {
                EmployeeId = id,
                Name = name,
                Role = role
            });
        }
    }

    [RelayCommand]
    private void AddEmployee()
    {
        if (string.IsNullOrWhiteSpace(NewEmployee.Name))
        {
            Console.WriteLine("⚠️ Name is required.");
            return;
        }

        try
        {
            // EmployeeId is a GUID by default
            db.InsertEmployee(
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
    private void UpdateEmployee()
    {
        if (SelectedEmployee is null) return;

        db.UpdateEmployee(
            SelectedEmployee.EmployeeId,
            SelectedEmployee.Name,
            SelectedEmployee.Role,
            SelectedEmployee.RequestedHours,
            SelectedEmployee.Availability,
            SelectedEmployee.ContractedHours
        );

        // Optional: refresh list
        LoadEmployees();
    }

    [RelayCommand]
    private void DeleteEmployee()
    {
        if (SelectedEmployee is null) return;

        db.DeleteEmployee(SelectedEmployee.EmployeeId);
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
