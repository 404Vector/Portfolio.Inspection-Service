using InspectionClient.Models;

namespace InspectionClient.Interfaces;

public interface IEquipmentConfigService
{
  EquipmentConfig Config { get; }
  void Save();
}
