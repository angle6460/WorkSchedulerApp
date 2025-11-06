using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WorkSchedulerApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WorkSchedulerApp.Data;
using WorkSchedulerApp.Factories;
using MainViewModel = WorkSchedulerApp.ViewModels.MainViewModel;
using PageViewModel = WorkSchedulerApp.ViewModels.PageViewModel;
using SettingsPageViewModel = WorkSchedulerApp.ViewModels.SettingsPageViewModel;

namespace WorkSchedulerApp;

using MainViewModel = MainViewModel;
using PageViewModel = PageViewModel;
using SettingsPageViewModel = SettingsPageViewModel;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<MainViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<WorkLoadsPageViewModel>();
        collection.AddTransient<WeeklyWorkLoadsPageViewModel>();
        collection.AddTransient<WeeklySchedulePageViewModel>();
        collection.AddTransient<EmployeesPageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();

        collection.AddSingleton<Func<ApplicationPageNames, PageViewModel>>(x => name => name switch
        {
            ApplicationPageNames.Home => x.GetRequiredService<HomePageViewModel>(),
            ApplicationPageNames.WorkLoads => x.GetRequiredService<WorkLoadsPageViewModel>(),
            ApplicationPageNames.WeeklyTemplates => x.GetRequiredService<WeeklyWorkLoadsPageViewModel>(),
            ApplicationPageNames.WeeklySchedules => x.GetRequiredService<WeeklySchedulePageViewModel>(),
            ApplicationPageNames.Employees => x.GetRequiredService<EmployeesPageViewModel>(),
            ApplicationPageNames.Settings => x.GetRequiredService<SettingsPageViewModel>(),
            _ => throw new InvalidOperationException()
            
        });
        
        collection.AddSingleton<PageFactory>();
        
        
        var services = collection.BuildServiceProvider();
        
        
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainView
            {
                DataContext = services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}