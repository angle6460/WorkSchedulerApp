using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Factories;

namespace WorkSchedulerApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private PageFactory _pageFactory;
    
    private const string buttonActiveClass = "active";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideMenuImage))]
    [NotifyPropertyChangedFor(nameof(SideMenuSize))]
    private bool _sideMenuExpanded = true;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomePageIsActive))]
    [NotifyPropertyChangedFor(nameof(WorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(DailyWorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(WeeklyWorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(EmployeesPageIsActive))]
    [NotifyPropertyChangedFor(nameof(SettingPageIsActive))]
    private PageViewModel _currentPage;
    
    public bool HomePageIsActive => CurrentPage.PageName == ApplicationPageNames.Home;
    public bool WorkPageIsActive => CurrentPage.PageName == ApplicationPageNames.WorkLoads;
    public bool DailyWorkPageIsActive => CurrentPage.PageName == ApplicationPageNames.WeeklyTemplates;
    public bool WeeklyWorkPageIsActive => CurrentPage.PageName ==  ApplicationPageNames.WeeklySchedules;
    public bool EmployeesPageIsActive => CurrentPage.PageName == ApplicationPageNames.Employees;
    public bool SettingPageIsActive => CurrentPage.PageName == ApplicationPageNames.Settings;
    
    

    public string SideMenuImage => $"/Assets/Images/{(SideMenuExpanded ? "Logo" : "Icon")}.svg";
    public string SideMenuSize => $"{(SideMenuExpanded ? "100" : "50")}";
    
    [RelayCommand]
    private void SideMenuResize()
    {
        SideMenuExpanded = !SideMenuExpanded;
    }
    
    /// <summary>
    /// Design-time only constructor
    /// </summary>
    public MainViewModel()
    {
        CurrentPage = new SettingsPageViewModel();
    }
    

    public MainViewModel(PageFactory pageFactory)
    {
        _pageFactory = pageFactory;
        GoToHome();
    }

    [RelayCommand]
    private void GoToHome()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Home);
    }

    [RelayCommand]
    private void GoToWorkLoads()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.WorkLoads);    }

    [RelayCommand]
    private void GoToWeeklyTemplates()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.WeeklyTemplates);    }

    [RelayCommand]
    private void GoToWeeklySchedules()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.WeeklySchedules);    }

    [RelayCommand]
    private void GoToEmployeesPage()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Employees);    }

    [RelayCommand]
    private void GoToSettingPage()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Settings);    }

}