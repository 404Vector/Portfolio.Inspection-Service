using System;
using System.IO;
using System.Text.Json;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Services;

public sealed class EquipmentConfigService : IEquipmentConfigService
{
  private static readonly string ConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "InspectionClient",
    "equipment-config.json");

  private static readonly JsonSerializerOptions JsonOptions = new() {
    WriteIndented = true,
  };

  public EquipmentConfig Config { get; }

  public EquipmentConfigService() {
    Config = Load();
  }

  public void Save() {
    var dir = Path.GetDirectoryName(ConfigPath)!;
    Directory.CreateDirectory(dir);
    var json = JsonSerializer.Serialize(Config, JsonOptions);
    File.WriteAllText(ConfigPath, json);
  }

  private static EquipmentConfig Load() {
    if (!File.Exists(ConfigPath))
      return new EquipmentConfig();

    try {
      var json = File.ReadAllText(ConfigPath);
      return JsonSerializer.Deserialize<EquipmentConfig>(json) ?? new EquipmentConfig();
    }
    catch (Exception) {
      return new EquipmentConfig();
    }
  }
}
