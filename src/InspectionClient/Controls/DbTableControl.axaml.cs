using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;

namespace InspectionClient.Controls;

/// <summary>
/// DB 테이블 목록 + CRUD 버튼을 표시하는 순수 UI 컨트롤.
/// ViewModel 의존성 없이 StyledProperty만으로 동작한다.
///
/// 상태 전환:
///   LoadedItem=null  → Browse 패널 (Load / New / Delete)
///   LoadedItem!=null → Edit   패널 (Save / Cancel)
///
/// 책임 경계:
///   - 컨트롤: LoadedItem 관리(Load 시 set, Save/Cancel 후 null), 패널 전환
///   - ViewModel: SaveCommand/CancelCommand를 통한 실제 데이터 변경
/// </summary>
public partial class DbTableControl : UserControl
{
  // ── StyledProperties ──────────────────────────────────────────────────

  public static readonly StyledProperty<string> HeaderProperty =
      AvaloniaProperty.Register<DbTableControl, string>(nameof(Header), defaultValue: string.Empty);

  public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
      AvaloniaProperty.Register<DbTableControl, IEnumerable?>(nameof(ItemsSource));

  public static readonly StyledProperty<object?> SelectedItemProperty =
      AvaloniaProperty.Register<DbTableControl, object?>(nameof(SelectedItem));

  public static readonly StyledProperty<object?> LoadedItemProperty =
      AvaloniaProperty.Register<DbTableControl, object?>(nameof(LoadedItem));

  public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
      AvaloniaProperty.Register<DbTableControl, IDataTemplate?>(nameof(ItemTemplate));

  public static readonly StyledProperty<ICommand?> LoadCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(LoadCommand));

  public static readonly StyledProperty<ICommand?> CreateCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(CreateCommand));

  public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(DeleteCommand));

  public static readonly StyledProperty<ICommand?> SaveCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(SaveCommand));

  public static readonly StyledProperty<ICommand?> CancelCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(CancelCommand));

  public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
      AvaloniaProperty.Register<DbTableControl, ICommand?>(nameof(RefreshCommand));

  // ── CLR プロパティ ──────────────────────────────────────────────────────

  public string      Header       { get => GetValue(HeaderProperty);       set => SetValue(HeaderProperty, value); }
  public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty);  set => SetValue(ItemsSourceProperty, value); }
  public object?     SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

  /// <summary>
  /// Load/더블클릭 시 SelectedItem이 복사된다. null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  public object?     LoadedItem   { get => GetValue(LoadedItemProperty);   set => SetValue(LoadedItemProperty, value); }

  /// <summary>LoadedItem != null 여부. 패널 전환 상태를 나타낸다.</summary>
  public bool        IsItemLoaded => LoadedItem is not null;

  public IDataTemplate? ItemTemplate  { get => GetValue(ItemTemplateProperty);  set => SetValue(ItemTemplateProperty, value); }
  public ICommand?      LoadCommand   { get => GetValue(LoadCommandProperty);   set => SetValue(LoadCommandProperty, value); }
  public ICommand?      CreateCommand { get => GetValue(CreateCommandProperty); set => SetValue(CreateCommandProperty, value); }
  public ICommand?      DeleteCommand { get => GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }
  public ICommand?      SaveCommand   { get => GetValue(SaveCommandProperty);   set => SetValue(SaveCommandProperty, value); }
  public ICommand?      CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
  public ICommand?      RefreshCommand{ get => GetValue(RefreshCommandProperty);set => SetValue(RefreshCommandProperty, value); }

  // ── 초기화 ───────────────────────────────────────────────────────────

  public DbTableControl()
  {
    InitializeComponent();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    ApplyAllToView();
    ItemListBox.SelectionChanged += OnSelectionChanged;
    ItemListBox.DoubleTapped     += OnDoubleTapped;
    LoadButton.Click             += OnLoadClicked;
    SaveButton.Click             += OnSaveClicked;
    CancelButton.Click           += OnCancelClicked;
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    ItemListBox.SelectionChanged -= OnSelectionChanged;
    ItemListBox.DoubleTapped     -= OnDoubleTapped;
    LoadButton.Click             -= OnLoadClicked;
    SaveButton.Click             -= OnSaveClicked;
    CancelButton.Click           -= OnCancelClicked;
    base.OnDetachedFromVisualTree(e);
  }

  // ── StyledProperty 변경 → 내부 뷰 동기화 ─────────────────────────────

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
  {
    base.OnPropertyChanged(change);

    // named elements는 InitializeComponent() 이후부터 유효하다.
    if (ItemListBox is null)
      return;

    if      (change.Property == HeaderProperty)       HeaderTextBlock.Text        = change.GetNewValue<string>();
    else if (change.Property == ItemsSourceProperty)  ItemListBox.ItemsSource     = change.GetNewValue<IEnumerable?>();
    else if (change.Property == SelectedItemProperty) ItemListBox.SelectedItem    = change.NewValue;
    else if (change.Property == ItemTemplateProperty) ItemListBox.ItemTemplate    = change.GetNewValue<IDataTemplate?>();
    else if (change.Property == LoadedItemProperty)   ApplyLoadedState(change.NewValue is not null);
    else if (change.Property == LoadCommandProperty)  LoadButton.Command          = change.GetNewValue<ICommand?>();
    else if (change.Property == CreateCommandProperty)CreateButton.Command        = change.GetNewValue<ICommand?>();
    else if (change.Property == DeleteCommandProperty)DeleteButton.Command        = change.GetNewValue<ICommand?>();
    else if (change.Property == SaveCommandProperty)  SaveButton.Command          = change.GetNewValue<ICommand?>();
    else if (change.Property == CancelCommandProperty)CancelButton.Command        = change.GetNewValue<ICommand?>();
    else if (change.Property == RefreshCommandProperty)RefreshButton.Command      = change.GetNewValue<ICommand?>();
  }

  // ── 이벤트 핸들러 ────────────────────────────────────────────────────

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
      SetCurrentValue(SelectedItemProperty, ItemListBox.SelectedItem);

  private void OnDoubleTapped(object? sender, RoutedEventArgs e)
  {
    if (SelectedItem is not null)
      TriggerLoad();
  }

  private void OnLoadClicked  (object? sender, RoutedEventArgs e) => TriggerLoad();

  private void OnSaveClicked  (object? sender, RoutedEventArgs e) =>
      SetCurrentValue(LoadedItemProperty, null);

  private void OnCancelClicked(object? sender, RoutedEventArgs e) =>
      SetCurrentValue(LoadedItemProperty, null);

  // ── 내부 로직 ─────────────────────────────────────────────────────────

  private void TriggerLoad()
  {
    if (SelectedItem is null)
      return;
    SetCurrentValue(LoadedItemProperty, SelectedItem);
    if (LoadCommand?.CanExecute(SelectedItem) == true)
      LoadCommand.Execute(SelectedItem);
  }

  /// <summary>
  /// Attach 시점에 모든 StyledProperty 값을 내부 뷰에 일괄 적용한다.
  /// OnPropertyChanged는 Attach 이전 설정분을 놓칠 수 있기 때문에 필요하다.
  /// </summary>
  private void ApplyAllToView()
  {
    HeaderTextBlock.Text      = Header;
    ItemListBox.ItemsSource   = ItemsSource;
    ItemListBox.SelectedItem  = SelectedItem;
    ItemListBox.ItemTemplate  = ItemTemplate;
    LoadButton.Command        = LoadCommand;
    CreateButton.Command      = CreateCommand;
    DeleteButton.Command      = DeleteCommand;
    SaveButton.Command        = SaveCommand;
    CancelButton.Command      = CancelCommand;
    RefreshButton.Command     = RefreshCommand;
    ApplyLoadedState(LoadedItem is not null);
  }

  private void ApplyLoadedState(bool isLoaded)
  {
    BrowsePanel.IsVisible = !isLoaded;
    EditPanel.IsVisible   =  isLoaded;
  }
}
