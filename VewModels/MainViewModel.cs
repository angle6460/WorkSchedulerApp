using System.Windows.Input;
using Avalonia.Svg.Skia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkScedulerApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SideMenuImage))]
    [NotifyPropertyChangedFor(nameof(SideMenuSize))]
    private bool _sideMenuExpanded = false;

    public string SideMenuImage => $"Assets/Images/{(SideMenuExpanded ? "Logo" : "Icon")}.svg";
    public string SideMenuSize => $"{(SideMenuExpanded ? "100" : "50")}";
    
    [RelayCommand]
    private void SideMenuResize()
    {
        SideMenuExpanded = !SideMenuExpanded;
    }

}