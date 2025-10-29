
using CommunityToolkit.Mvvm.ComponentModel;
using WorkScedulerApp.Data;

namespace WorkScedulerApp.ViewModels;

public partial class PageViewModel : ViewModelBase
{
    [ObservableProperty] 
    private ApplicationPageNames _pageName;
}