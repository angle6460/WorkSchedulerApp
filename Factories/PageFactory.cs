using System;
using WorkScedulerApp.Data;
using WorkScedulerApp.ViewModels;

namespace WorkScedulerApp.Factories;

public class PageFactory(Func<ApplicationPageNames, PageViewModel> factory)
{
    public PageViewModel GetPageViewModel(ApplicationPageNames pageName) => factory.Invoke(pageName);
}