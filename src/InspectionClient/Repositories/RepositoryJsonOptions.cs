using System.Text.Json;
using System.Text.Json.Serialization;

namespace InspectionClient.Repositories;

internal static class RepositoryJsonOptions
{
  internal static readonly JsonSerializerOptions Default = new()
  {
    WriteIndented = false,
    Converters    = { new JsonStringEnumConverter() },
  };
}
