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
/// WaferInfo 목록의 읽기 전용 뷰 — DbTableControl의 Load/Refresh 전용.
/// RecipeSetupWorkflow에서 Wafer 선택에 사용된다.
/// DbTableControl이 LoadedItem 상태를 자체 관리하므로 IsItemLoaded 불필요.
/// </summary>
public sealed partial class WaferInfoListViewModel : ViewModelBase
{
  private readonly IWaferInfoRepository _repository;

  public ObservableCollection<WaferInfoRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  private WaferInfoRow? _selectedItem;

  /// <summary>Load 버튼 클릭 또는 더블클릭 시 발생한다.</summary>
  public event EventHandler<WaferInfoRow>? WaferSelected;

  public WaferInfoListViewModel(
      IWaferInfoRepository repository,
      ILogService          logService)
      : base(logService)
  {
    _repository = repository;
    _ = RefreshAsync();
  }

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
    if (SelectedItem is not WaferInfoRow row)
      return;
    WaferSelected?.Invoke(this, row);
    // DbTableControl이 LoadedItem을 SelectedItem으로 set한다.
    // ViewModel은 TwoWay 바인딩으로 동기화만 수행한다.
  }, nameof(Load));

  private bool HasSelectedItem => SelectedItem is not null;
}
