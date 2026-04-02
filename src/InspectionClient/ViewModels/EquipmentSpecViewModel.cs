using System.Linq;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class EquipmentSpecViewModel : ViewModelBase
{
  public EquipmentConfig Spec { get; }

  public string MagnificationsText =>
      string.Join(", ", Spec.Magnifications.Select(m => $"{m}×"));

  public EquipmentSpecViewModel(ILogService logService, IEquipmentConfigService configService)
    : base(logService) {
    Spec = configService.Config;
  }
}
