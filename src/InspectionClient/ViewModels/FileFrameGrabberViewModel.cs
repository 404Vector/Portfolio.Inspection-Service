using Core.Logging.Interfaces;
using Core.Grpc.FrameGrabber;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class FileFrameGrabberViewModel : FrameGrabberViewModelBase
{
  public FileFrameGrabberViewModel(
      ILogService logService,
      IFrameGrabberController controller,
      IFrameSource frameSource,
      IServiceConnectionMonitor connectionMonitor)
      : base(logService, controller, frameSource, connectionMonitor, CreateParameters())
  {
  }

  private static GrabberParameterItem[] CreateParameters() =>
  [
    new() { Key = "frame_rate_hz",    DisplayName = "Frame Rate (Hz)",  ValueType = ParameterValueType.Double, MinValue = 1.0,  MaxValue = 1000.0, CurrentValue = 30.0,         OriginalValue = 30.0         },
    new() { Key = "width",            DisplayName = "Width (px)",       ValueType = ParameterValueType.Int64,  MinValue = 1L,   MaxValue = 16384L, CurrentValue = 1280L,        OriginalValue = 1280L        },
    new() { Key = "height",           DisplayName = "Height (px)",      ValueType = ParameterValueType.Int64,  MinValue = 1L,   MaxValue = 16384L, CurrentValue = 1024L,        OriginalValue = 1024L        },
    new() { Key = "pixel_format",     DisplayName = "Pixel Format",     ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "Mono8",      OriginalValue = "Mono8"      },
    new() { Key = "acquisition_mode", DisplayName = "Acquisition Mode", ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "Continuous", OriginalValue = "Continuous" },
    new() { Key = "source_mode",      DisplayName = "Source Mode",      ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "gradient",   OriginalValue = "gradient"   },
    new() { Key = "image_path",       DisplayName = "Image Path",       ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "",           OriginalValue = ""           },
  ];
}
