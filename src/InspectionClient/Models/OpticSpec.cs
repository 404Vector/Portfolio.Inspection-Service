using System.ComponentModel;

namespace InspectionClient.Models;

/// <summary>
/// 장비 설치 시 결정되는 HW 불변 스펙. 런타임에 변경되지 않는다.
/// </summary>
public class OpticSpec
{
  // ── Sensor ───────────────────────────────────────────────

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

  // ── Objective ────────────────────────────────────────────

  [Category("Objective")]
  [DisplayName("Magnification (×)")]
  [Description("Magnification of the mounted objective lens.")]
  public double Magnification { get; set; } = 10.0;

  [Category("Objective")]
  [DisplayName("Effective Pixel Size (μm/px)")]
  [Description("Derived from pixel pitch ÷ magnification.")]
  public double EffectivePixelSizeUm { get; set; } = 0.345;

  // ── Illumination ─────────────────────────────────────────

  [Category("Illumination")]
  [DisplayName("Illumination Type")]
  [Description("Brightfield / Darkfield / Coaxial")]
  public string IlluminationType { get; set; } = "Coaxial";
}
