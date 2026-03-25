using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    private readonly ILogService _log;

    public HistoryViewModel(ILogService logService)
    {
        _log = logService;
    }
}
