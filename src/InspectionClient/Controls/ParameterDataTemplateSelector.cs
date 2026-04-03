using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Core.Grpc.FrameGrabber;
using InspectionClient.Models;

namespace InspectionClient.Controls;

public class ParameterDataTemplateSelector : IDataTemplate
{
  public IDataTemplate? Int64Template  { get; set; }
  public IDataTemplate? DoubleTemplate { get; set; }
  public IDataTemplate? BoolTemplate   { get; set; }
  public IDataTemplate? EnumTemplate   { get; set; }
  public IDataTemplate? BytesTemplate  { get; set; }
  public IDataTemplate? StringTemplate { get; set; }

  public Control? Build(object? param)
  {
    if (param is not GrabberParameterItem item) return null;

    var template = item.ValueType switch
    {
      ParameterValueType.Int64  => Int64Template,
      ParameterValueType.Double => DoubleTemplate,
      ParameterValueType.Bool   => BoolTemplate,
      ParameterValueType.Bytes  => BytesTemplate,
      ParameterValueType.String when item.AllowedValues is { Count: > 0 } => EnumTemplate,
      _                         => StringTemplate,
    };

    return template?.Build(param);
  }

  public bool Match(object? data) => data is GrabberParameterItem;
}
