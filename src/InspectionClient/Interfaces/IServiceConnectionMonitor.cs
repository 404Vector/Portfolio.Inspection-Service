using System;
using System.Collections.Generic;

namespace InspectionClient.Interfaces;

/// <summary>
/// gRPC 서비스들의 연결 상태를 감시하고 변경을 통보한다.
/// </summary>
public interface IServiceConnectionMonitor
{
  /// <summary>
  /// 서비스 키(예: "FrameGrabber", "Inspection") → 현재 연결 여부.
  /// </summary>
  IReadOnlyDictionary<string, bool> States { get; }

  /// <summary>
  /// 연결 상태가 변경될 때 UI 스레드에서 발생한다.
  /// </summary>
  event EventHandler<ServiceConnectionChangedEventArgs> StateChanged;
}

public sealed record ServiceConnectionChangedEventArgs(string ServiceKey, bool IsConnected);
