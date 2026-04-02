using System;
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
///   - DieRenderingParameters CRUD (목록 로드, 생성, 저장, 삭제)
///   - 선택 항목을 DieRenderingControl에 연결
/// </summary>
public sealed partial class DieSetupWorkflowViewModel : ViewModelBase
{
  private readonly IDieRenderingParametersRepository _repository;

  public IDieImageRenderer Renderer { get; }

  // ── 목록 ─────────────────────────────────────────────────────────────

  public ObservableCollection<DieParametersRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
  private DieParametersRow? _selectedItem;

  /// <summary>
  /// 현재 편집 중인 항목. DbTableControl.LoadedItem과 양방향 바인딩.
  /// null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Parameters))]
  private DieParametersRow? _loadedItem;

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

  /// <summary>
  /// 현재 편집 중인 파라미터. LoadedItem이 없으면 기본값 인스턴스를 반환한다.
  /// WaferSetupWorkflow에서 DieSize 가져오기에 사용된다.
  /// </summary>
  public DieRenderingParameters Parameters =>
      LoadedItem?.Parameters ?? _defaultParameters;

  private readonly DieRenderingParameters _defaultParameters = new();

  public DieSetupWorkflowViewModel(
      IDieRenderingParametersRepository repository,
      IDieImageRenderer                 renderer,
      ILogService                       logService)
      : base(logService)
  {
    _repository = repository;
    Renderer    = renderer;
    _ = RefreshAsync();
  }

  // ── 커맨드 ───────────────────────────────────────────────────────────

  [RelayCommand]
  private async Task RefreshAsync() => await Execute(async () =>
  {
    var list = await _repository.ListAsync();
    Items.Clear();
    foreach (var item in list)
      Items.Add(item);
  }, nameof(RefreshAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedItem))]
  private void Load(object? item) => Execute(() =>
  {
    // DbTableControl이 LoadedItem을 SelectedItem으로 set한다.
    // ViewModel은 TwoWay 바인딩으로 동기화만 수행한다.
  }, nameof(Load));

  [RelayCommand]
  private async Task CreateAsync() => await Execute(async () =>
  {
    var name = $"New-{DateTime.Now:yyMMdd-HHmmss}";
    var row  = await _repository.CreateAsync(name);
    await RefreshAsync();
    SelectedItem = FindById(row.Id);
  }, nameof(CreateAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedItem))]
  private async Task DeleteAsync() => await Execute(async () =>
  {
    if (SelectedItem is not DieParametersRow current)
      return;
    await _repository.DeleteAsync(current.Id);
    Items.Remove(current);
    SelectedItem = null;
  }, nameof(DeleteAsync));

  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    if (LoadedItem is not DieParametersRow current)
      return;
    await _repository.UpdateAsync(current);
    LoadedItem = null;
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task CancelAsync() => await Execute(async () =>
  {
    if (LoadedItem is not DieParametersRow current)
      return;
    var restored = await _repository.FindByIdAsync(current.Id);
    if (restored is not null)
    {
      var idx = Items.IndexOf(current);
      if (idx >= 0)
        Items[idx] = restored;
      SelectedItem = restored;
    }
    LoadedItem = null;
  }, nameof(CancelAsync));

  private bool HasSelectedItem => SelectedItem is not null;

  private DieParametersRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }
}
