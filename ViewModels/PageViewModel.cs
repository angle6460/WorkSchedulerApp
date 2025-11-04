using CommunityToolkit.Mvvm.ComponentModel;
using WorkSchedulerApp.Data;

namespace WorkSchedulerApp.ViewModels;

public partial class PageViewModel : ViewModelBase
{
    [ObservableProperty] 
    private ApplicationPageNames _pageName;
}