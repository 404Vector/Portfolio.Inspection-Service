using System.ComponentModel;
using Core.Enums;

namespace InspectionClient.Models;

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
    [DisplayName("Pixel Size (μm/px)")]
    [Description("Effective pixel size derived from magnification and sensor pixel pitch.")]
    public double PixelSizeUm { get; set; } = 0.55;

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
    [DisplayName("Frame Count")]
    [Description("Number of frames to acquire per inspection sequence.")]
    public int FrameCount { get; set; } = 1;

    [Category("Frame Grabber")]
    [DisplayName("Acquisition Timeout (ms)")]
    [Description("Maximum wait time for frame acquisition.")]
    public int AcquisitionTimeoutMs { get; set; } = 3000;

    [Category("Frame Grabber")]
    [DisplayName("Trigger Mode")]
    [Description("Software / Hardware")]
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Software;

    [Category("Frame Grabber")]
    [DisplayName("Trigger Delay (μs)")]
    [Description("Delay from trigger signal reception to exposure start.")]
    public int TriggerDelayUs { get; set; } = 1000;

    [Category("Frame Grabber")]
    [DisplayName("Trigger Activation")]
    [Description("RisingEdge / FallingEdge")]
    public TriggerActivation TriggerActivation { get; set; } = TriggerActivation.RisingEdge;

    // ── Objective ────────────────────────────────────────────

    [Category("Objective")]
    [DisplayName("Magnification (×)")]
    [Description("Magnification of the mounted objective lens. Used for scale conversion.")]
    public ObjectiveMagnification ObjectiveMagnification { get; set; } = ObjectiveMagnification.X10;

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
