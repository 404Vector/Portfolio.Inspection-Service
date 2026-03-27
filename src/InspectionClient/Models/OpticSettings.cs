using System.ComponentModel;
using Core.Enums;

namespace InspectionClient.Models;

/// <summary>
/// 유저가 조정하는 광학계 설정값. Recipe 성격의 런타임 파라미터.
/// </summary>
public class OpticSettings
{
  // ── Camera ───────────────────────────────────────────────

  [Category("Camera")]
  [DisplayName("Exposure Time (μs)")]
  [Description("Sensor exposure time. Shorter values reduce motion blur.")]
  public int ExposureUs { get; set; } = 5000;

  [Category("Camera")]
  [DisplayName("Gain (dB)")]
  [Description("Analog gain. Trade-off with noise.")]
  public double Gain { get; set; } = 0.0;

  [Category("Camera")]
  [DisplayName("Black Level")]
  [Description("Sensor output offset.")]
  public int BlackLevel { get; set; } = 0;

  [Category("Camera")]
  [DisplayName("Pixel Format")]
  [Description("Mono8 / Mono10 / Mono12")]
  public PixelFormat PixelFormat { get; set; } = PixelFormat.Mono8;

  [Category("Camera")]
  [DisplayName("Image Width (px)")]
  [Description("Captured frame width in pixels.")]
  public int ImageWidth { get; set; } = 1024;

  [Category("Camera")]
  [DisplayName("Image Height (px)")]
  [Description("Captured frame height in pixels.")]
  public int ImageHeight { get; set; } = 1024;

  // ── Frame Grabber ────────────────────────────────────────

  [Category("Frame Grabber")]
  [DisplayName("Frame Rate (Hz)")]
  [Description("Target acquisition frame rate.")]
  public double FrameRateHz { get; set; } = 30.0;

  [Category("Frame Grabber")]
  [DisplayName("Acquisition Timeout (ms)")]
  [Description("Maximum wait time for frame acquisition.")]
  public int AcquisitionTimeoutMs { get; set; } = 3000;

  [Category("Frame Grabber")]
  [DisplayName("Trigger Mode")]
  [Description("Software / Hardware")]
  public TriggerMode TriggerMode { get; set; } = TriggerMode.Software;

  // ── Illumination ─────────────────────────────────────────

  [Category("Illumination")]
  [DisplayName("Intensity (%)")]
  [Description("Light output level. Range: 0 – 100.")]
  public int LightIntensity { get; set; } = 80;

  [Category("Illumination")]
  [DisplayName("Mode")]
  [Description("Continuous / Strobe")]
  public LightMode LightMode { get; set; } = LightMode.Strobe;
}
