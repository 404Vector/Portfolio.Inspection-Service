using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Enums;
using Core.Models;
using InspectionClient.Interfaces;

namespace InspectionClient.Models;

/// <summary>
/// WaferInfo 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// WaferInfo 필드를 flat하게 보유하여 View에서 직접 바인딩 가능하다.
/// DieParametersId: 연결된 DieRenderingParameters의 FK (nullable).
/// </summary>
public sealed class WaferInfoRow : ObservableObject, IRowId
{
  public long   Id   { get; init; }
  public string Name { get; set; } = string.Empty;

  private string           _waferId          = "WAFER-001";
  private string           _lotId            = "LOT-001";
  private int              _slotIndex        = 1;
  private WaferType        _waferType        = WaferType.Wafer300mm;
  private double           _thicknessUm      = 775.0;
  private WaferGrade       _waferGrade       = WaferGrade.Dummy;
  private NotchOrientation _notchOrientation = NotchOrientation.Down;
  private double           _dieSizeWidthUm   = 10_000.0;
  private double           _dieSizeHeightUm  = 10_000.0;
  private double           _dieOffsetXum     = 0.0;
  private double           _dieOffsetYum     = 0.0;
  private double           _edgeOffsetUm     = 3_000.0;
  private string           _processStep      = "Unknown";

  public string           WaferId          { get => _waferId;          set => SetProperty(ref _waferId,          value); }
  public string           LotId            { get => _lotId;            set => SetProperty(ref _lotId,            value); }
  public int              SlotIndex        { get => _slotIndex;        set => SetProperty(ref _slotIndex,        value); }
  public WaferType        WaferType        { get => _waferType;        set => SetProperty(ref _waferType,        value); }
  public double           ThicknessUm      { get => _thicknessUm;      set => SetProperty(ref _thicknessUm,      value); }
  public WaferGrade       WaferGrade       { get => _waferGrade;       set => SetProperty(ref _waferGrade,       value); }
  public NotchOrientation NotchOrientation { get => _notchOrientation; set => SetProperty(ref _notchOrientation, value); }
  public double           DieSizeWidthUm   { get => _dieSizeWidthUm;   set => SetProperty(ref _dieSizeWidthUm,   value); }
  public double           DieSizeHeightUm  { get => _dieSizeHeightUm;  set => SetProperty(ref _dieSizeHeightUm,  value); }
  public double           DieOffsetXum     { get => _dieOffsetXum;     set => SetProperty(ref _dieOffsetXum,     value); }
  public double           DieOffsetYum     { get => _dieOffsetYum;     set => SetProperty(ref _dieOffsetYum,     value); }
  public double           EdgeOffsetUm     { get => _edgeOffsetUm;     set => SetProperty(ref _edgeOffsetUm,     value); }
  public string           ProcessStep      { get => _processStep;      set => SetProperty(ref _processStep,      value); }

  public long? DieParametersId { get; set; }

  /// <summary>현재 필드 값으로 WaferInfo record를 조립한다.</summary>
  public WaferInfo ToWaferInfo() => new(
    WaferId:          WaferId,
    LotId:            LotId,
    SlotIndex:        SlotIndex,
    WaferType:        WaferType,
    ThicknessUm:      ThicknessUm,
    Grade:            WaferGrade,
    NotchOrientation: NotchOrientation,
    CoordinateOrigin: WaferCoordinate.Origin,
    DieSize:          new DieSize(DieSizeWidthUm, DieSizeHeightUm),
    DieOffset:        new WaferCoordinate(DieOffsetXum, DieOffsetYum),
    EdgeOffsetUm:     EdgeOffsetUm,
    ProcessStep:      ProcessStep,
    CreatedAt:        DateTimeOffset.UtcNow
  );

  /// <summary>WaferInfo record의 값을 이 인스턴스의 필드에 복사한다.</summary>
  public void LoadFrom(WaferInfo info)
  {
    WaferId          = info.WaferId;
    LotId            = info.LotId;
    SlotIndex        = info.SlotIndex;
    WaferType        = info.WaferType;
    ThicknessUm      = info.ThicknessUm;
    WaferGrade       = info.Grade;
    NotchOrientation = info.NotchOrientation;
    DieSizeWidthUm   = info.DieSize.WidthUm;
    DieSizeHeightUm  = info.DieSize.HeightUm;
    DieOffsetXum     = info.DieOffset.Xum;
    DieOffsetYum     = info.DieOffset.Yum;
    EdgeOffsetUm     = info.EdgeOffsetUm;
    ProcessStep      = info.ProcessStep;
  }
}
