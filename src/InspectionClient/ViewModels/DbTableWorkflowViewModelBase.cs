using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

/// <summary>
/// DbTableControl 기반 워크플로 ViewModel의 추상 베이스.
///
/// 제공하는 기능:
///   - Items / SelectedItem / LoadedItem 프로퍼티
///   - Refresh / Create / Load / Save / Delete / Cancel 커맨드
///   - FindById 헬퍼
///
/// 파생 클래스는 <see cref="OnLoaded"/>와 <see cref="OnBeforeSave"/>를
/// override하여 편집 버퍼 연동 등 고유 로직을 추가한다.
/// </summary>
public abstract partial class DbTableWorkflowViewModelBase<TRow> : ViewModelBase
    where TRow : class, IRowId
{
  private readonly INamedRepository<TRow> _repository;

  // ── 목록 ─────────────────────────────────────────────────────────────

  public ObservableCollection<TRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
  private TRow? _selectedItem;

  /// <summary>
  /// 현재 편집 중인 항목. DbTableControl.LoadedItem과 양방향 바인딩.
  /// null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  private TRow? _loadedItem;
  public TRow? LoadedItem
  {
    get => _loadedItem;
    set
    {
      if (SetProperty(ref _loadedItem, value))
        OnLoadedItemUpdated(value);
    }
  }

  protected DbTableWorkflowViewModelBase(
      INamedRepository<TRow> repository,
      ILogService logService)
      : base(logService)
  {
    _repository = repository;
  }

  // ── CRUD 커맨드 ──────────────────────────────────────────────────────

  [RelayCommand]
  protected async Task RefreshAsync() => await Execute(async () =>
  {
    var list = await _repository.ListAsync();
    Items.Clear();
    foreach (var item in list)
      Items.Add(item);
  }, nameof(RefreshAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedItem))]
  private void Load(object? item) => Execute(() =>
  {
    if (SelectedItem is not TRow row)
      return;
    OnLoaded(row);
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
    if (SelectedItem is not TRow current)
      return;
    await _repository.DeleteAsync(current.Id);
    Items.Remove(current);
    SelectedItem = null;
  }, nameof(DeleteAsync));

  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    if (LoadedItem is not TRow current)
      return;
    OnBeforeSave(current);
    await _repository.UpdateAsync(current);
    LoadedItem = null;
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task CancelAsync() => await Execute(async () =>
  {
    if (LoadedItem is not TRow current)
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

  // ── 파생 클래스 훅 ──────────────────────────────────────────────────

  /// <summary>
  /// Load 커맨드 실행 시 호출된다.
  /// 편집 버퍼에 값을 채우는 등의 작업을 수행한다.
  /// 기본 구현은 아무 작업도 하지 않는다 (TwoWay 바인딩으로 충분한 경우).
  /// </summary>
  protected virtual void OnLoaded(TRow row) { }

  /// <summary>
  /// Save 커맨드 실행 시, UpdateAsync 호출 직전에 호출된다.
  /// BuildRecipe 등으로 row를 갱신하는 작업을 수행한다.
  /// 기본 구현은 아무 작업도 하지 않는다 (직접 바인딩으로 충분한 경우).
  /// </summary>
  protected virtual void OnBeforeSave(TRow row) { }

  /// <summary>
  /// LoadedItem 프로퍼티가 변경될 때 호출된다.
  /// 파생 클래스에서 추가 PropertyChanged 알림 등에 사용한다.
  /// </summary>
  protected virtual void OnLoadedItemUpdated(TRow? value) { }

  // ── CanExecute 헬퍼 ─────────────────────────────────────────────────

  private bool HasSelectedItem => SelectedItem is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  protected TRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }
}
