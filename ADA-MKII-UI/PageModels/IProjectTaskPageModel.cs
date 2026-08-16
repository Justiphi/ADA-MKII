using ADA_MKII_UI.Models;
using CommunityToolkit.Mvvm.Input;

namespace ADA_MKII_UI.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}