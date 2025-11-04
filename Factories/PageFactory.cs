using System;
using WorkSchedulerApp.ViewModels;
using WorkSchedulerApp.Data;
using PageViewModel = WorkSchedulerApp.ViewModels.PageViewModel;
using VewModels_PageViewModel = WorkSchedulerApp.ViewModels.PageViewModel;

namespace WorkSchedulerApp.Factories;

public class PageFactory(Func<ApplicationPageNames, VewModels_PageViewModel> factory)
{
    public VewModels_PageViewModel GetPageViewModel(ApplicationPageNames pageName) => factory.Invoke(pageName);
}