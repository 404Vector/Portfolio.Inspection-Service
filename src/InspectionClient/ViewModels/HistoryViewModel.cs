using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
  private readonly IInspectionResultRepository _resultRepository;

  public ObservableCollection<InspectionResultEntry> ResultList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(DeleteSelectedResultCommand))]
  private InspectionResultEntry? _selectedResult;

  public HistoryViewModel(
      IInspectionResultRepository resultRepository,
      ILogService logService) : base(logService) {
    _resultRepository = resultRepository;
  }

  // ── 커맨드 ─────────────────────────────────────────────────────────────

  [RelayCommand]
  private async Task RefreshResultListAsync() => await Execute(async () =>
  {
    var list = await _resultRepository.ListEntriesAsync();
    ResultList.Clear();
    foreach (var item in list)
      ResultList.Add(item);
  }, nameof(RefreshResultListAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedResult))]
  private async Task DeleteSelectedResultAsync() => await Execute(async () =>
  {
    var toDelete = SelectedResult!;
    await _resultRepository.DeleteAsync(toDelete.ResultId);
    SelectedResult = null;
    await RefreshResultListAsync();
  }, nameof(DeleteSelectedResultAsync));

  // ── CanExecute 헬퍼 ───────────────────────────────────────────────────

  private bool HasSelectedResult => SelectedResult is not null;
}
