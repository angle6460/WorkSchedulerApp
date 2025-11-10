using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Database;

namespace WorkSchedulerApp.ViewModels;

public partial class WorkLoadsPageViewModel : PageViewModel
{
    private readonly DatabaseHandler db;

    [ObservableProperty]
    private ObservableCollection<WorkLoadVM> workloads = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkLoadIsGroup))]
    private WorkLoadVM? selectedWorkload;
    
    public bool WorkLoadIsGroup => selectedWorkload?.WorkLoadType == "Group";

    [ObservableProperty]
    private ObservableCollection<WorkLoadVM> groupChildren = new();
    [ObservableProperty]
    private ObservableCollection<WorkLoadVM> availableWorkloadsForGroup = new();

    [ObservableProperty]
    private WorkLoadVM? selectedAvailableWorkload;
    public bool WorkLoadIsFixed => SelectedWorkload?.WorkLoadType == "Fixed";
    public bool WorkLoadIsPerItem => SelectedWorkload?.WorkLoadType == "PerItem";
    public bool WorkLoadIsPerEmployee => SelectedWorkload?.WorkLoadType == "PerEmployee";
    public bool WorkLoadIsNotGroup => SelectedWorkload?.WorkLoadType != "Group";
    public bool HasSelection => SelectedWorkload is not null;



    [ObservableProperty]
    private WorkLoadVM? selectedChild;


    public WorkLoadsPageViewModel()
    {
        PageName = ApplicationPageNames.WorkLoads;
        db = DatabaseHandler.Instance;

        _ = LoadWorkloadsAsync();
    }

    // ------------------------------------------------------------
    // LOAD ALL WORKLOAD TEMPLATE SUMMARY + DETAILS
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task LoadWorkloadsAsync()
    {
        Workloads.Clear();

        var rows = await db.GetAllWorkLoadTemplatesAsync();

        foreach (var (id, _, _) in rows)
        {
            var full = await db.GetWorkLoadTemplateFullAsync(id);
            if (full is null) continue;

            Workloads.Add(new WorkLoadVM
            {
                Id = id,
                TemplateName = full.Value.name,
                TemplateDescription = full.Value.description,
                WorkLoadType = full.Value.type,
                EstimatedHours = full.Value.estimatedHours,

                HoursPerItem = full.Value.minutesPerItem / 60.0,
                ItemCount = full.Value.numberOfItems,

                HoursPerEmployee = full.Value.minutesPerEmployee / 60.0,
                EmployeeCount = full.Value.numberOfEmployees

            });

        }
    }

    partial void OnSelectedWorkloadChanged(WorkLoadVM? value)
    {
        OnPropertyChanged(nameof(WorkLoadIsGroup));
        OnPropertyChanged(nameof(WorkLoadIsNotGroup));
        OnPropertyChanged(nameof(WorkLoadIsFixed));
        OnPropertyChanged(nameof(WorkLoadIsPerItem));
        OnPropertyChanged(nameof(WorkLoadIsPerEmployee));
        OnPropertyChanged(nameof(HasSelection));
        _ = LoadGroupChildrenAsync();
    }


    // ------------------------------------------------------------
    // LOAD GROUP CHILDREN
    // ------------------------------------------------------------
    private async Task LoadGroupChildrenAsync()
    {
        GroupChildren.Clear();
        AvailableWorkloadsForGroup.Clear();

        if (SelectedWorkload == null)
            return;

        if (SelectedWorkload.WorkLoadType != "Group")
            return;

        // Load current children
        var childIds = await db.GetGroupChildrenAsync(SelectedWorkload.Id);

        foreach (var childId in childIds)
        {
            var row = await db.GetWorkLoadTemplateByIdAsync(childId);
            if (row is null) continue;

            GroupChildren.Add(new WorkLoadVM
            {
                Id = childId,
                TemplateName = row.Value.name,
                TemplateDescription = row.Value.description,
                WorkLoadType = row.Value.type,
                EstimatedHours = row.Value.estimatedHours
            });
        }

        //  Recalculate total hours of the group
        double total = 0;
        foreach (var child in GroupChildren)
            total += child.EstimatedHours;

        SelectedWorkload.EstimatedHours = total;

        //  Refresh left-side workload list so UI updates
        await LoadWorkloadsAsync();


        // Load available non-group templates not already selected
        var all = await db.GetAllWorkLoadTemplatesAsync();
        foreach (var (id, name, type) in all)
        {
            if (type == "Group") continue;              // can't add groups
            if (childIds.Contains(id)) continue;        // avoid duplicates

            var full = await db.GetWorkLoadTemplateByIdAsync(id);
            if (full is null) continue;

            AvailableWorkloadsForGroup.Add(new WorkLoadVM
            {
                Id = id,
                TemplateName = full.Value.name,
                TemplateDescription = full.Value.description,
                WorkLoadType = full.Value.type,
                EstimatedHours = full.Value.estimatedHours
            });
        }
    }



    // ------------------------------------------------------------
    // CREATE NEW WORKLOAD TEMPLATE
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task AddFixedWorkloadAsync()
    {
        int id = await db.InsertFixedWorkLoadTemplateAsync(
            "New Fixed Workload", "Description", 1
        );
        await ReloadAndSelectAsync(id);
    }

    [RelayCommand]
    public async Task AddPerItemWorkloadAsync()
    {
        int id = await db.InsertPerItemWorkLoadTemplateAsync(
            "New Per-Item Workload", "Description", 1, 10
        );
        await ReloadAndSelectAsync(id);
    }

    [RelayCommand]
    public async Task AddPerEmployeeWorkloadAsync()
    {
        int id = await db.InsertPerEmployeeWorkLoadTemplateAsync(
            "New Per-Employee Workload", "Description", 2, 6
        );
        await ReloadAndSelectAsync(id);
    }

    [RelayCommand]
    public async Task AddGroupWorkloadAsync()
    {
        int id = await db.InsertGroupWorkLoadTemplateAsync(
            "New Group Workload", "Description"
        );
        await ReloadAndSelectAsync(id);
    }

    private async Task ReloadAndSelectAsync(int id)
    {
        await LoadWorkloadsAsync();
        SelectedWorkload = Workloads.FirstOrDefault(w => w.Id == id);
    }

    // ------------------------------------------------------------
    // SAVE SELECTED WORKLOAD
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task SaveWorkloadAsync()
    {
        if (SelectedWorkload == null)
            return;

        switch (SelectedWorkload.WorkLoadType)
        {
            case "Fixed":
                await db.UpdateWorkLoadTemplateAsync(
                    SelectedWorkload.Id,
                    SelectedWorkload.TemplateName,
                    SelectedWorkload.TemplateDescription,
                    SelectedWorkload.EstimatedHours
                );
                break;

            case "PerItem":
                await db.UpdatePerItemWorkLoadTemplateAsync(
                    SelectedWorkload.Id,
                    SelectedWorkload.TemplateName,
                    SelectedWorkload.TemplateDescription,
                    SelectedWorkload.HoursPerItem,
                    SelectedWorkload.ItemCount
                );
                break;

            case "PerEmployee":
                await db.UpdatePerEmployeeWorkLoadTemplateAsync(
                    SelectedWorkload.Id,
                    SelectedWorkload.TemplateName,
                    SelectedWorkload.TemplateDescription,
                    SelectedWorkload.HoursPerEmployee,
                    SelectedWorkload.EmployeeCount
                );
                break;

            case "Group":
                // Name + description only (hours auto-computed)
                await db.UpdateWorkLoadTemplateAsync(
                    SelectedWorkload.Id,
                    SelectedWorkload.TemplateName,
                    SelectedWorkload.TemplateDescription,
                    SelectedWorkload.EstimatedHours
                );
                break;
        }

        await LoadWorkloadsAsync();
    }


    // ------------------------------------------------------------
    // DELETE SELECTED WORKLOAD
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task DeleteWorkloadAsync()
    {
        if (SelectedWorkload == null)
            return;

        await db.DeleteWorkLoadTemplateAsync(SelectedWorkload.Id);

        SelectedWorkload = null;
        GroupChildren.Clear();

        await LoadWorkloadsAsync();
    }

    // ------------------------------------------------------------
    // GROUP CHILD MANAGEMENT
    // ------------------------------------------------------------
    [RelayCommand]
    public async Task AddChildToGroupAsync()
    {
        if (SelectedWorkload == null || SelectedAvailableWorkload == null)
            return;
        if (SelectedWorkload.WorkLoadType != "Group")
            return;

        await db.AddChildToGroupAsync(
            SelectedWorkload.Id,
            SelectedAvailableWorkload.Id
        );

        // Recalculate hours in DB and update VM
        SelectedWorkload.EstimatedHours =
            await db.GetEstimatedHoursRecursiveAsync(SelectedWorkload.Id);

        await LoadGroupChildrenAsync();
    }



    [RelayCommand]
    public async Task RemoveSelectedChildAsync()
    {
        if (SelectedWorkload == null || SelectedChild == null)
            return;

        await db.RemoveChildFromGroupAsync(SelectedWorkload.Id, SelectedChild.Id);

        // Recalculate group hours
        SelectedWorkload.EstimatedHours =
            await db.GetEstimatedHoursRecursiveAsync(SelectedWorkload.Id);

        await LoadGroupChildrenAsync();
    }

}

// ------------------------------------------------------------
// SUPPORT MODEL FOR UI
// ------------------------------------------------------------
public partial class WorkLoadVM : ObservableObject
{
    [ObservableProperty] public int id;

    [ObservableProperty] public string templateName = "";
    [ObservableProperty] public string templateDescription = "";

    [ObservableProperty] public string workLoadType = "";

    // Common
    [ObservableProperty] public double estimatedHours;

    // Per-Item
    [ObservableProperty] public double hoursPerItem;
    [ObservableProperty] public int itemCount;

    // Per-Employee
    [ObservableProperty] public double hoursPerEmployee;
    [ObservableProperty] public int employeeCount;
}

