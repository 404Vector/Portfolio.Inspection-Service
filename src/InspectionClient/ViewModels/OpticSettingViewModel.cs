using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

public partial class OpticSettingViewModel : ViewModelBase
{
    private readonly ILogService _log;

    public OpticSettingViewModel(ILogService logService)
    {
        _log = logService;
    }
}
