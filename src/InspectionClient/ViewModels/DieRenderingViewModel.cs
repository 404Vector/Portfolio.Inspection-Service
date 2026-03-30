using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// DieRenderingView의 ViewModel.
///
/// DieRenderingParameters가 ObservableObject이므로 View가 Parameters의 프로퍼티에
/// 직접 바인딩한다. ViewModel은 Parameters 인스턴스를 단독 소유하며 교체하지 않는다.
///
/// Apply: 현재 편집 값을 Renderer.Save()에 snapshot으로 저장한다.
///        CurrentParameters와 현재 값이 다를 때만 활성화된다.
/// Reset: 마지막으로 저장된 snapshot을 Parameters에 복사한다.
/// </summary>
public partial class DieRenderingViewModel : ViewModelBase
{
  // ── 렌더러 (View의 DieRenderingControl에 바인딩) ─────────────────────
  public IDieImageRenderer Renderer { get; }

  // ── 파라미터 범위 (View NumericUpDown 바인딩용) ───────────────────────
  public int CanvasWidthMin    => DieRenderingParameters.Limits.CanvasWidthMin;
  public int CanvasWidthMax    => DieRenderingParameters.Limits.CanvasWidthMax;
  public int CanvasHeightMin   => DieRenderingParameters.Limits.CanvasHeightMin;
  public int CanvasHeightMax   => DieRenderingParameters.Limits.CanvasHeightMax;
  public int BackgroundGrayMin => DieRenderingParameters.Limits.BackgroundGrayMin;
  public int BackgroundGrayMax => DieRenderingParameters.Limits.BackgroundGrayMax;
  public int PadRowCountMin    => DieRenderingParameters.Limits.PadRowCountMin;
  public int PadRowCountMax    => DieRenderingParameters.Limits.PadRowCountMax;
  public int PadColumnCountMin => DieRenderingParameters.Limits.PadColumnCountMin;
  public int PadColumnCountMax => DieRenderingParameters.Limits.PadColumnCountMax;

  // ── 렌더링 파라미터 (DieRenderingControl 및 View 입력 컨트롤에 바인딩) ─
  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
  private DieRenderingParameters _parameters = new();

  public DieRenderingViewModel(IDieImageRenderer renderer, ILogService logService)
      : base(logService)
  {
    Renderer = renderer;
    _parameters.PropertyChanged += OnParametersPropertyChanged;
  }

  // ── Parameters 내부 변경 감지 ─────────────────────────────────────────
  // Parameters 참조 교체는 [NotifyCanExecuteChangedFor]가 처리한다.
  // 내부 프로퍼티 변경은 PropertyChanged 이벤트로 직접 수신한다.

  partial void OnParametersChanged(DieRenderingParameters? oldValue, DieRenderingParameters newValue)
  {
    if (oldValue is not null)
      oldValue.PropertyChanged -= OnParametersPropertyChanged;

    newValue.PropertyChanged += OnParametersPropertyChanged;
  }

  private void OnParametersPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    ApplyCommand.NotifyCanExecuteChanged();
  }

  // ── CanExecute ────────────────────────────────────────────────────────

  /// <summary>
  /// CurrentParameters가 없으면 항상 활성화.
  /// CurrentParameters가 있으면 현재 편집 값과 다를 때만 활성화.
  /// </summary>
  private bool CanApply()
  {
    var saved = Renderer.CurrentParameters;
    return saved is null || !Parameters.ValueEquals(saved);
  }

  // ── 커맨드 ───────────────────────────────────────────────────────────

  /// <summary>
  /// 현재 파라미터를 Renderer에 snapshot으로 저장한다.
  /// </summary>
  [RelayCommand(CanExecute = nameof(CanApply))]
  private void Apply() => Execute(() =>
  {
    Renderer.Save(Parameters);
    ApplyCommand.NotifyCanExecuteChanged();
  }, nameof(Apply));

  /// <summary>
  /// 마지막으로 저장된 snapshot을 Parameters에 복사한다.
  /// 저장된 값이 없으면 기본값으로 초기화한다.
  /// </summary>
  [RelayCommand]
  private void Reset() => Execute(() =>
  {
    var saved = Renderer.CurrentParameters ?? new DieRenderingParameters();
    Parameters.CopyFrom(saved);
  }, nameof(Reset));
}
