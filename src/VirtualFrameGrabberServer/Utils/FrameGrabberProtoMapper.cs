using Core.Grpc.FrameGrabber;
using Core.SharedMemory.Models;

using DomainAcquisitionMode   = Core.Enums.AcquisitionMode;
using DomainPixelFormat        = Core.Enums.PixelFormat;
using DomainGrabberState       = Core.Enums.GrabberState;
using DomainParameterValue     = Core.FrameGrabber.Models.ParameterValue;
using DomainParameterValueType = Core.FrameGrabber.Models.ParameterValueType;
using DomainCommandResult      = Core.FrameGrabber.Models.CommandResult;

using ProtoParameterValue      = Core.Grpc.FrameGrabber.ParameterValue;
using ProtoCommandResult       = Core.Grpc.FrameGrabber.CommandResult;
using ProtoParameterDescriptor = Core.Grpc.FrameGrabber.ParameterDescriptor;
using ProtoCommandDescriptor   = Core.Grpc.FrameGrabber.CommandDescriptor;
using ProtoParameterValueType  = Core.Grpc.FrameGrabber.ParameterValueType;

namespace VirtualFrameGrabberServer.Utils;

internal static class FrameGrabberProtoMapper
{
  internal static FrameHandle ToProtoHandle(FrameInfo info) => new()
  {
    FrameId         = info.FrameId,
    SharedMemoryKey = info.SharedMemoryKey,
    TimestampUs     = info.TimestampUs,
    Width           = info.Width,
    Height          = info.Height,
    PixelFormat     = ToProtoPixelFormat(info.PixelFormat),
    Stride          = info.Stride,
    SizeBytes       = info.SizeBytes,
    SlotIndex       = info.SlotIndex,
    Sequence        = info.Sequence
  };

  internal static DomainAcquisitionMode ToMode(AcquisitionMode proto) => proto switch
  {
    AcquisitionMode.Triggered => DomainAcquisitionMode.Triggered,
    _                         => DomainAcquisitionMode.Continuous
  };

  internal static DomainPixelFormat ToPixelFormat(PixelFormat proto) => proto switch
  {
    PixelFormat.Rgb8 => DomainPixelFormat.Rgb8,
    PixelFormat.Bgr8 => DomainPixelFormat.Bgr8,
    _                => DomainPixelFormat.Mono8
  };

  internal static AcquisitionMode ToProtoMode(DomainAcquisitionMode mode) => mode switch
  {
    DomainAcquisitionMode.Triggered => AcquisitionMode.Triggered,
    _                               => AcquisitionMode.Continuous
  };

  internal static PixelFormat ToProtoPixelFormat(DomainPixelFormat fmt) => fmt switch
  {
    DomainPixelFormat.Rgb8 => PixelFormat.Rgb8,
    DomainPixelFormat.Bgr8 => PixelFormat.Bgr8,
    _                      => PixelFormat.Mono8
  };

  internal static GrabberState ToProtoState(DomainGrabberState state) => state switch
  {
    DomainGrabberState.Acquiring => GrabberState.Acquiring,
    DomainGrabberState.Error     => GrabberState.Error,
    _                            => GrabberState.Idle
  };

  internal static ProtoParameterValueType ToProtoParameterValueType(DomainParameterValueType t) => t switch
  {
    DomainParameterValueType.Int64  => ProtoParameterValueType.Int64,
    DomainParameterValueType.Double => ProtoParameterValueType.Double,
    DomainParameterValueType.Bool   => ProtoParameterValueType.Bool,
    DomainParameterValueType.String => ProtoParameterValueType.String,
    DomainParameterValueType.Bytes  => ProtoParameterValueType.Bytes,
    _                               => ProtoParameterValueType.Unspecified
  };

  /// <summary>
  /// ParameterDescriptor의 MinValue/MaxValue/DefaultValue(object?)를 proto ParameterValue로 변환.
  /// </summary>
  internal static ProtoParameterValue ToProtoParameterValueFromRaw(object rawValue, DomainParameterValueType type)
  {
    var proto = new ProtoParameterValue();
    switch (type)
    {
      case DomainParameterValueType.Int64:  proto.IntVal    = Convert.ToInt64(rawValue);   break;
      case DomainParameterValueType.Double: proto.DoubleVal = Convert.ToDouble(rawValue);  break;
      case DomainParameterValueType.Bool:   proto.BoolVal   = Convert.ToBoolean(rawValue); break;
      case DomainParameterValueType.String: proto.StringVal = rawValue.ToString() ?? string.Empty; break;
    }
    return proto;
  }

  internal static ProtoParameterValue ToProtoValue(DomainParameterValue v) => v switch
  {
    DomainParameterValue.Int64Value  i => new ProtoParameterValue { IntVal    = i.Value },
    DomainParameterValue.DoubleValue d => new ProtoParameterValue { DoubleVal = d.Value },
    DomainParameterValue.BoolValue   b => new ProtoParameterValue { BoolVal   = b.Value },
    DomainParameterValue.StringValue s => new ProtoParameterValue { StringVal = s.Value },
    DomainParameterValue.BytesValue  b => new ProtoParameterValue { BytesVal  = Google.Protobuf.ByteString.CopyFrom(b.Value) },
    _                                  => new ProtoParameterValue()
  };

  internal static DomainParameterValue ToDomainValue(ProtoParameterValue proto) =>
      proto.ValueCase switch
      {
        ProtoParameterValue.ValueOneofCase.IntVal    => new DomainParameterValue.Int64Value(proto.IntVal),
        ProtoParameterValue.ValueOneofCase.DoubleVal => new DomainParameterValue.DoubleValue(proto.DoubleVal),
        ProtoParameterValue.ValueOneofCase.BoolVal   => new DomainParameterValue.BoolValue(proto.BoolVal),
        ProtoParameterValue.ValueOneofCase.StringVal => new DomainParameterValue.StringValue(proto.StringVal),
        ProtoParameterValue.ValueOneofCase.BytesVal  => new DomainParameterValue.BytesValue(proto.BytesVal.ToByteArray()),
        _ => throw new ArgumentException("ParameterValue has no value set")
      };

  internal static ProtoCommandResult ToProtoCommandResult(DomainCommandResult result)
  {
    var proto = new ProtoCommandResult { Success = result.Success };
    switch (result.ReturnValue)
    {
      case DomainParameterValue.Int64Value  i: proto.IntVal    = i.Value; break;
      case DomainParameterValue.DoubleValue d: proto.DoubleVal = d.Value; break;
      case DomainParameterValue.BoolValue   b: proto.BoolVal   = b.Value; break;
      case DomainParameterValue.StringValue s: proto.StringVal = s.Value; break;
    }
    return proto;
  }
}
