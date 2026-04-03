using System;
using Core.Grpc.FrameGrabber;
using Core.SharedMemory.Models;
using InspectionClient.Models;

using ProtoPixelFormat  = Core.Grpc.FrameGrabber.PixelFormat;
using DomainPixelFormat = Core.Enums.PixelFormat;

namespace InspectionClient.Utils;

internal static class GrabberProtoMapper
{
  internal static object? ToObject(ParameterValue v) => v.ValueCase switch
  {
    ParameterValue.ValueOneofCase.IntVal    => v.IntVal,
    ParameterValue.ValueOneofCase.DoubleVal => v.DoubleVal,
    ParameterValue.ValueOneofCase.BoolVal   => v.BoolVal,
    ParameterValue.ValueOneofCase.StringVal => v.StringVal,
    ParameterValue.ValueOneofCase.BytesVal  => v.BytesVal.ToByteArray(),
    _                                       => null
  };

  internal static FrameInfo ToFrameInfo(FrameHandle h) => new(
      FrameId:         h.FrameId,
      SlotIndex:       h.SlotIndex,
      SharedMemoryKey: h.SharedMemoryKey,
      TimestampUs:     h.TimestampUs,
      Width:           h.Width,
      Height:          h.Height,
      PixelFormat:     ToPixelFormat(h.PixelFormat),
      Stride:          h.Stride,
      SizeBytes:       h.SizeBytes,
      Sequence:        h.Sequence);

  internal static DomainPixelFormat ToPixelFormat(ProtoPixelFormat fmt) => fmt switch
  {
    ProtoPixelFormat.Rgb8 => DomainPixelFormat.Rgb8,
    ProtoPixelFormat.Bgr8 => DomainPixelFormat.Bgr8,
    _                     => DomainPixelFormat.Mono8,
  };

  internal static ParameterValue ToParameterValue(object? value, ParameterValueType hint = ParameterValueType.String) =>
      value switch
      {
        long    l => new ParameterValue { IntVal    = l },
        int     i => new ParameterValue { IntVal    = i },
        double  d => new ParameterValue { DoubleVal = d },
        float   f => new ParameterValue { DoubleVal = f },
        bool    b => new ParameterValue { BoolVal   = b },
        decimal m => hint == ParameterValueType.Int64
            ? new ParameterValue { IntVal    = (long)m }
            : new ParameterValue { DoubleVal = (double)m },
        Enum    e => new ParameterValue { StringVal = e.ToString() },
        _         => new ParameterValue { StringVal = value?.ToString() ?? string.Empty },
      };
}
