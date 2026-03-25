using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

public partial class InspectionViewModel : ViewModelBase
{
    private readonly ILogService _log;

    public InspectionViewModel(ILogService logService)
    {
        _log = logService;
    }
}
