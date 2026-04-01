using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace InspectionClient.Models;

/// <summary>
/// 설비 전체의 HW 고정 스펙 및 식별 정보.
/// JSON 파일로 저장·로드하며 런타임에 변경되지 않는다.
/// </summary>
public class EquipmentConfig
{
  // ── Identity ─────────────────────────────────────────────

  [JsonPropertyOrder(-1)]
  public int SchemaVersion { get; set; } = 1;

  [Category("Identity")]
  [DisplayName("Equipment ID")]
  [Description("Unique identifier of this equipment.")]
  public string EquipmentId { get; set; } = "EQ-001";

  [Category("Identity")]
  [DisplayName("Model Name")]
  [Description("Equipment model name.")]
  public string ModelName { get; set; } = "SEQ-1000";

  [Category("Identity")]
  [DisplayName("Serial Number")]
  [Description("Equipment serial number.")]
  public string SerialNumber { get; set; } = "SN-20260001";

  [Category("Identity")]
  [DisplayName("Location")]
  [Description("Physical installation location.")]
  public string Location { get; set; } = "KR.SS001.Line-A";

  // ── Optic ─────────────────────────────────────────────────

  [Category("Sensor")]
  [DisplayName("Sensor Width (px)")]
  [Description("Physical sensor width in pixels.")]
  public int SensorWidth { get; set; } = 4096;

  [Category("Sensor")]
  [DisplayName("Sensor Height (px)")]
  [Description("Physical sensor height in pixels.")]
  public int SensorHeight { get; set; } = 3000;

  [Category("Sensor")]
  [DisplayName("Pixel Pitch (μm)")]
  [Description("Physical size of one sensor pixel.")]
  public double PixelPitchUm { get; set; } = 3.45;

  [Category("Objective")]
  [DisplayName("Magnifications (×)")]
  [Description("Available objective lens magnifications.")]
  public List<uint> Magnifications { get; set; } = [2, 5, 10];

  [Category("Illumination")]
  [DisplayName("Illumination Type")]
  [Description("Brightfield / Darkfield / Coaxial")]
  public string IlluminationType { get; set; } = "Coaxial";

  // ── Stage ─────────────────────────────────────────────────

  [Category("Stage")]
  [DisplayName("Travel X (mm)")]
  [Description("X-axis travel range.")]
  public double TravelXMm { get; set; } = 300.0;

  [Category("Stage")]
  [DisplayName("Travel Y (mm)")]
  [Description("Y-axis travel range.")]
  public double TravelYMm { get; set; } = 300.0;

  [Category("Stage")]
  [DisplayName("Travel Z (mm)")]
  [Description("Z-axis travel range.")]
  public double TravelZMm { get; set; } = 50.0;

  [Category("Stage")]
  [DisplayName("Max Speed (mm/s)")]
  [Description("Maximum stage movement speed.")]
  public double MaxSpeedMmPerSec { get; set; } = 100.0;

  [Category("Stage")]
  [DisplayName("Max Acceleration (mm/s²)")]
  [Description("Maximum stage acceleration.")]
  public double MaxAccelMmPerSec2 { get; set; } = 500.0;

  [Category("Stage")]
  [DisplayName("Repeatability (μm)")]
  [Description("Positional repeatability.")]
  public double RepeatabilityUm { get; set; } = 1.0;

  [Category("Stage")]
  [DisplayName("Accuracy (μm)")]
  [Description("Positional accuracy.")]
  public double AccuracyUm { get; set; } = 2.0;

  // ── Illumination Controller ───────────────────────────────

  [Category("Illumination Controller")]
  [DisplayName("Channels")]
  [Description("Number of light controller output channels.")]
  public int LightControllerChannels { get; set; } = 4;

  [Category("Illumination Controller")]
  [DisplayName("Max Current (mA)")]
  [Description("Maximum output current per channel.")]
  public int LightControllerMaxCurrentMa { get; set; } = 1000;

  // ── Communication ─────────────────────────────────────────

  [Category("Communication")]
  [DisplayName("FrameGrabber Service Address")]
  [Description("gRPC address of the FrameGrabberService.")]
  public string FrameGrabberServiceAddress { get; set; } = "http://localhost:5100";

  [Category("Communication")]
  [DisplayName("Inspection Service Address")]
  [Description("gRPC address of the InspectionService.")]
  public string InspectionServiceAddress { get; set; } = "http://localhost:5200";
}
