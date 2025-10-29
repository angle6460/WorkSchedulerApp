using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WorkScedulerApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WorkScedulerApp.Data;
using WorkScedulerApp.Factories;

namespace WorkScedulerApp;

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
        collection.AddTransient<DailyWorkPageViewModel>();
        collection.AddTransient<EmployeesPageViewModel>();
        collection.AddTransient<HomePageViewModel>();
        collection.AddTransient<WeeklyWorkPageViewModel>();
        collection.AddTransient<WorkPageViewModel>();
        collection.AddTransient<SettingsPageViewModel>();

        collection.AddSingleton<Func<ApplicationPageNames, PageViewModel>>(x => name => name switch
        {
            ApplicationPageNames.Home => x.GetRequiredService<HomePageViewModel>(),
            ApplicationPageNames.DailyWork => x.GetRequiredService<DailyWorkPageViewModel>(),
            ApplicationPageNames.Employees => x.GetRequiredService<EmployeesPageViewModel>(),
            ApplicationPageNames.Settings => x.GetRequiredService<SettingsPageViewModel>(),
            ApplicationPageNames.WeeklyWork => x.GetRequiredService<WeeklyWorkPageViewModel>(),
            ApplicationPageNames.Work => x.GetRequiredService<WorkPageViewModel>(),
            _ => throw new InvalidOperationException()
            
        });
        
        collection.AddSingleton<PageFactory>();
        
        
        var services = collection.BuildServiceProvider();
        
        
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainView
            {
                DataContext = services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}