using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

public partial class InspectionViewModel : ViewModelBase
{
    public InspectionViewModel(ILogService logService) : base(logService) { }
}
