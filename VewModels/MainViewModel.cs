using CommunityToolkit.Mvvm.ComponentModel;

namespace BatchProcces3.VeiwModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _test = "test";
}