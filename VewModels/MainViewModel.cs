using System.Windows.Input;
using Avalonia.Svg.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkScedulerApp.Data;
using WorkScedulerApp.Factories;
using WorkScedulerApp.Views;

namespace WorkScedulerApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private PageFactory _pageFactory;
    
    private const string buttonActiveClass = "active";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideMenuImage))]
    [NotifyPropertyChangedFor(nameof(SideMenuSize))]
    private bool _sideMenuExpanded = false;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomePageIsActive))]
    [NotifyPropertyChangedFor(nameof(WorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(DailyWorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(WeeklyWorkPageIsActive))]
    [NotifyPropertyChangedFor(nameof(EmployeesPageIsActive))]
    [NotifyPropertyChangedFor(nameof(SettingPageIsActive))]
    private PageViewModel _currentPage;
    
    public bool HomePageIsActive => CurrentPage.PageName == ApplicationPageNames.Home;
    public bool WorkPageIsActive => CurrentPage.PageName == ApplicationPageNames.Work;
    public bool DailyWorkPageIsActive => CurrentPage.PageName == ApplicationPageNames.DailyWork;
    public bool WeeklyWorkPageIsActive => CurrentPage.PageName ==  ApplicationPageNames.WeeklyWork;
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
    private void GoToWork()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Work);    }

    [RelayCommand]
    private void GoToDailyWork()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.DailyWork);    }

    [RelayCommand]
    private void GoToWeeklyWork()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.WeeklyWork);    }

    [RelayCommand]
    private void GoToEmployeesPage()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Employees);    }

    [RelayCommand]
    private void GoToSettingPage()
    {
        CurrentPage = _pageFactory.GetPageViewModel(ApplicationPageNames.Settings);    }

}