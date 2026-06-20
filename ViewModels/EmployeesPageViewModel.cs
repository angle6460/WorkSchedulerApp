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
    private ObservableCollection<Employee> employees = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoolSelectedEmployee))]
    [NotifyPropertyChangedFor(nameof(EditingEmployee))]
    private Employee? selectedEmployee;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditingEmployee))]
    private Employee newEmployee = new();

    // UI SKILLS LIST (ViewModel-owned)
    [ObservableProperty]
    private ObservableCollection<WorkLoadTemplateVM> skillsForUI = new();

    // Available skills to add
    [ObservableProperty]
    private ObservableCollection<WorkLoadTemplateVM> availableSkills = new();

    [ObservableProperty]
    private WorkLoadTemplateVM? selectedAvailableSkill;

    private readonly DatabaseHandler db;

    public EmployeesPageViewModel()
    {
        PageName = ApplicationPageNames.Employees;

        db = DatabaseHandler.Instance;

        _ = LoadEmployeesAsync();
        _ = LoadAvailableSkillsAsync();
    }

    public Employee EditingEmployee => SelectedEmployee ?? NewEmployee;
    public bool BoolSelectedEmployee => SelectedEmployee is not null;
    public bool CanDelete => SelectedEmployee is not null;

    // --------------------------------------------------------------------
    // Load employees
    // --------------------------------------------------------------------
    [RelayCommand]
    private async Task LoadEmployeesAsync()
    {
        Employees.Clear();

        var rows = await db.GetAllEmployeesAsync();

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

    // --------------------------------------------------------------------
    // Load all workload templates as "skills"
    // --------------------------------------------------------------------
    private async Task LoadAvailableSkillsAsync()
    {
        AvailableSkills.Clear();

        var list = await db.GetAllWorkLoadTemplatesAsync();

        foreach (var (id, name, type) in list)
        {
            AvailableSkills.Add(new WorkLoadTemplateVM
            {
                Id = id,
                Name = name,
                Type = type
            });
        }
    }

    // --------------------------------------------------------------------
    // When selecting an employee, load their skills
    // --------------------------------------------------------------------
    partial void OnSelectedEmployeeChanged(Employee? emp)
    {
        if (emp != null)
            _ = LoadSkillsForSelectedAsync();
        else
            SkillsForUI.Clear();
    }

    private async Task LoadSkillsForSelectedAsync()
    {
        SkillsForUI.Clear();

        if (SelectedEmployee == null)
            return;

        var links = await db.GetAllEmployeeTemplateSkillsAsync();
        var templates = await db.GetAllWorkLoadTemplatesAsync();

        // Find all template IDs for this employee
        foreach (var (employeeId, templateId) in links)
        {
            if (employeeId == SelectedEmployee.EmployeeId)
            {
                var tpl = templates.First(t => t.id == templateId);

                SkillsForUI.Add(new WorkLoadTemplateVM
                {
                    Id = tpl.id,
                    Name = tpl.name,
                    Type = tpl.type
                });
            }
        }
    }

    // --------------------------------------------------------------------
    // Add skill
    // --------------------------------------------------------------------
    [RelayCommand]
    public async Task AddSkillAsync()
    {
        if (SelectedEmployee == null || SelectedAvailableSkill == null)
            return;

        await db.AddTemplateSkillToEmployeeAsync(
            SelectedEmployee.EmployeeId,
            SelectedAvailableSkill.Id
        );

        await LoadSkillsForSelectedAsync();
    }

    // --------------------------------------------------------------------
    // Remove skill
    // --------------------------------------------------------------------
    [RelayCommand]
    public async Task RemoveSkillAsync(WorkLoadTemplateVM skill)
    {
        if (SelectedEmployee == null) return;

        await db.RemoveTemplateSkillFromEmployeeAsync(
            SelectedEmployee.EmployeeId,
            skill.Id
        );

        await LoadSkillsForSelectedAsync();
    }

    // --------------------------------------------------------------------
    // Add employee
    // --------------------------------------------------------------------
    [RelayCommand]
    private async Task AddEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEmployee.Name))
            return;

        await db.InsertEmployeeAsync(
            NewEmployee.EmployeeId,
            NewEmployee.Name,
            NewEmployee.Role,
            NewEmployee.RequestedHours,
            NewEmployee.Availability,
            NewEmployee.ContractedHours
        );

        Employees.Add(NewEmployee);
        NewEmployee = new();
        SkillsForUI.Clear();
    }

    // --------------------------------------------------------------------
    // Update
    // --------------------------------------------------------------------
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

        await LoadEmployeesAsync();
        await LoadSkillsForSelectedAsync();
    }

    // --------------------------------------------------------------------
    // Delete
    // --------------------------------------------------------------------
    [RelayCommand]
    private async Task DeleteEmployeeAsync()
    {
        if (SelectedEmployee is null) return;

        await db.DeleteEmployeeAsync(SelectedEmployee.EmployeeId);

        Employees.Remove(SelectedEmployee);
        SelectedEmployee = null;
        SkillsForUI.Clear();
    }

    // --------------------------------------------------------------------
    // Clear fields
    // --------------------------------------------------------------------
    [RelayCommand]
    private void ClearFields()
    {
        SelectedEmployee = null;
        NewEmployee = new Employee();
        SkillsForUI.Clear();
    }
}
