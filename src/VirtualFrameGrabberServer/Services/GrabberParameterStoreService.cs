using Core.FrameGrabber.Models;
using CE = Core.Enums;

namespace VirtualFrameGrabberServer.Services;

/// <summary>
/// IFrameGrabber 파라미터 레지스트리와 GrabberConfig 바인딩을 담당한다.
/// GrabberConfig에 매핑되지 않는 파라미터(source_mode, image_path)는
/// 이 클래스가 상태를 보관하고, 처리는 VirtualFrameGrabberServer가 담당한다.
/// </summary>
public sealed class GrabberParameterStoreService
{
  // ── source_mode / image_path 상태 ────────────────────────────────────────
  // GrabberConfig에 속하지 않으므로 여기서 직접 보관한다.

  public string SourceMode { get; private set; } = "gradient";
  public string ImagePath  { get; private set; } = string.Empty;

  // ── 파라미터 레지스트리 ───────────────────────────────────────────────────

  private static readonly IReadOnlyList<ParameterDescriptor> SupportedParameters =
  [
    new("frame_rate_hz",    "Frame Rate (Hz)",    ParameterValueType.Double, MinValue: 1.0,  MaxValue: 1000.0, DefaultValue: 30.0),
    new("width",            "Width (px)",         ParameterValueType.Int64,  MinValue: 1L,   MaxValue: 16384L, DefaultValue: 1280L),
    new("height",           "Height (px)",        ParameterValueType.Int64,  MinValue: 1L,   MaxValue: 16384L, DefaultValue: 1024L),
    new("pixel_format",     "Pixel Format",       ParameterValueType.String, MinValue: null, MaxValue: null,   DefaultValue: "Mono8"),
    new("acquisition_mode", "Acquisition Mode",   ParameterValueType.String, MinValue: null, MaxValue: null,   DefaultValue: "Continuous"),
    new("source_mode",      "Source Mode",        ParameterValueType.String, MinValue: null, MaxValue: null,   DefaultValue: "gradient"),
    new("image_path",       "Image Path",         ParameterValueType.String, MinValue: null, MaxValue: null,   DefaultValue: ""),
  ];

  public IReadOnlyList<ParameterDescriptor> GetParameters() => SupportedParameters;

  public ParameterValue GetParameter(GrabberConfig config, string key) => key switch
  {
    "frame_rate_hz"    => new ParameterValue.DoubleValue(config.FrameRateHz),
    "width"            => new ParameterValue.Int64Value(config.Width),
    "height"           => new ParameterValue.Int64Value(config.Height),
    "pixel_format"     => new ParameterValue.StringValue(config.PixelFormat.ToString()),
    "acquisition_mode" => new ParameterValue.StringValue(config.Mode.ToString()),
    "source_mode"      => new ParameterValue.StringValue(SourceMode),
    "image_path"       => new ParameterValue.StringValue(ImagePath),
    _                  => throw new KeyNotFoundException($"Unknown parameter: '{key}'")
  };

  /// <summary>
  /// GrabberConfig에 매핑되는 파라미터를 적용한 새 GrabberConfig를 반환한다.
  /// source_mode / image_path는 이 메서드로 처리하지 않는다 — ApplySynthesizerParameter()를 사용한다.
  /// </summary>
  public GrabberConfig ApplyParameter(GrabberConfig config, string key, ParameterValue value) =>
    key switch
    {
      "frame_rate_hz" => value is ParameterValue.DoubleValue d
          ? config with { FrameRateHz = ValidateRange(d.Value, 1.0, 1000.0, key) }
          : throw new ArgumentException($"Expected Double for '{key}'"),

      "width" => value is ParameterValue.Int64Value w
          ? config with { Width = (int)ValidateRange(w.Value, 1, 16384, key) }
          : throw new ArgumentException($"Expected Int64 for '{key}'"),

      "height" => value is ParameterValue.Int64Value h
          ? config with { Height = (int)ValidateRange(h.Value, 1, 16384, key) }
          : throw new ArgumentException($"Expected Int64 for '{key}'"),

      "pixel_format" => value is ParameterValue.StringValue s
          ? config with { PixelFormat = ParsePixelFormat(s.Value) }
          : throw new ArgumentException($"Expected String for '{key}'"),

      "acquisition_mode" => value is ParameterValue.StringValue m
          ? config with { Mode = ParseAcquisitionMode(m.Value) }
          : throw new ArgumentException($"Expected String for '{key}'"),

      _ => throw new KeyNotFoundException($"Unknown parameter: '{key}'")
    };

  /// <summary>
  /// source_mode / image_path 파라미터를 적용한다.
  /// 처리 대상이면 true, 해당 없으면 false를 반환한다.
  /// </summary>
  public bool ApplySynthesizerParameter(string key, ParameterValue value)
  {
    switch (key)
    {
      case "source_mode":
        if (value is not ParameterValue.StringValue sm)
          throw new ArgumentException($"Expected String for '{key}'");
        if (sm.Value is not ("gradient" or "file"))
          throw new ArgumentException($"Invalid source_mode '{sm.Value}'. Valid values: gradient, file");
        SourceMode = sm.Value;
        return true;

      case "image_path":
        if (value is not ParameterValue.StringValue ip)
          throw new ArgumentException($"Expected String for '{key}'");
        ImagePath = ip.Value;
        return true;

      default:
        return false;
    }
  }

  private static T ValidateRange<T>(T value, T min, T max, string key)
      where T : IComparable<T>
  {
    if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
      throw new ArgumentException($"Parameter '{key}' value {value} is out of range [{min}, {max}]");
    return value;
  }

  private static CE.PixelFormat ParsePixelFormat(string value) => value switch
  {
    "Mono8" => CE.PixelFormat.Mono8,
    "Rgb8"  => CE.PixelFormat.Rgb8,
    "Bgr8"  => CE.PixelFormat.Bgr8,
    _       => throw new ArgumentException($"Unknown pixel format: '{value}'. Valid values: Mono8, Rgb8, Bgr8")
  };

  private static CE.AcquisitionMode ParseAcquisitionMode(string value) => value switch
  {
    "Continuous" => CE.AcquisitionMode.Continuous,
    "Triggered"  => CE.AcquisitionMode.Triggered,
    _            => throw new ArgumentException($"Unknown acquisition mode: '{value}'. Valid values: Continuous, Triggered")
  };
}
