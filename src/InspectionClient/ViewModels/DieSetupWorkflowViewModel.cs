using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Die Setup 워크플로 ViewModel.
///
/// 책임:
///   - DieRenderingParameters 편집
///   - Save: 현재 파라미터를 이름을 지정하여 DB에 저장
///   - Load: DB 목록에서 선택하여 편집 폼에 적용
///   - Delete: DB에서 선택 항목 삭제
/// </summary>
public partial class DieSetupWorkflowViewModel : ViewModelBase
{
  private readonly IDieRenderingParametersRepository _repository;

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

  [ObservableProperty] private DieRenderingParameters _parameters = new();

  // ── DB 저장/로드 ──────────────────────────────────────────────────────

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
  private string _saveName = string.Empty;

  [ObservableProperty] private string _savedDbName = "(미저장)";

  // ── 목록 패널 ───────────────────────────────────────────────────────────

  public ObservableCollection<DieParametersEntry> ParameterList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
  private DieParametersEntry? _selectedEntry;

  public DieSetupWorkflowViewModel(
      IDieRenderingParametersRepository repository,
      IDieImageRenderer                 renderer,
      ILogService                       logService)
      : base(logService)
  {
    _repository = repository;
    Renderer    = renderer;
  }

  // ── CanExecute ────────────────────────────────────────────────────────

  private bool CanSave()   => !string.IsNullOrWhiteSpace(SaveName);
  private bool HasSelected => SelectedEntry is not null;

  // ── 커맨드 ───────────────────────────────────────────────────────────

  [RelayCommand(CanExecute = nameof(CanSave))]
  private async Task SaveAsync() => await Execute(async () =>
  {
    await _repository.SaveAsync(SaveName, Parameters);
    SavedDbName = SaveName;
    await RefreshListAsync();
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task RefreshListAsync() => await Execute(async () =>
  {
    var entries = await _repository.ListAsync();
    ParameterList.Clear();
    foreach (var entry in entries)
      ParameterList.Add(entry);
  }, nameof(RefreshListAsync));

  [RelayCommand(CanExecute = nameof(HasSelected))]
  private void LoadSelected() => Execute(() =>
  {
    var entry = SelectedEntry!;
    Parameters.CopyFrom(entry.Parameters);
    SaveName    = entry.Name;
    SavedDbName = entry.Name;
  }, nameof(LoadSelected));

  [RelayCommand(CanExecute = nameof(HasSelected))]
  private async Task DeleteSelectedAsync() => await Execute(async () =>
  {
    await _repository.DeleteAsync(SelectedEntry!.Name);
    SelectedEntry = null;
    await RefreshListAsync();
  }, nameof(DeleteSelectedAsync));

  /// <summary>현재 편집 중인 파라미터를 반환한다. WaferSetupWorkflow에서 DieSize 가져오기에 사용.</summary>
  public DieRenderingParameters GetParameters() => Parameters;
}
