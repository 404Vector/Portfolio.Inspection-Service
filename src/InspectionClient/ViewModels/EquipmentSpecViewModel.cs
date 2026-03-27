using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class EquipmentSpecViewModel : ViewModelBase
{
  public EquipmentConfig Spec { get; }

  public EquipmentSpecViewModel(ILogService logService, IEquipmentConfigService configService)
    : base(logService) {
    Spec = configService.Config;
  }
}
