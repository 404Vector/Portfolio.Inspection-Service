using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;

namespace InspectionClient.Controls;

/// <summary>
/// 목록에서 항목을 선택하고 Load/Unload하는 경량 컨트롤.
///
/// 상태 전환:
///   LoadedItem=null  → Load 버튼 표시, 목록 활성화
///   LoadedItem!=null → Unload 버튼 표시, 목록 비활성화
///
/// DbTableControl과 달리 CRUD(Create/Delete/Save/Cancel)를 포함하지 않는다.
/// </summary>
public partial class SelectionListControl : UserControl
{
  // ── StyledProperties ──────────────────────────────────────────────────

  public static readonly StyledProperty<string> HeaderProperty =
      AvaloniaProperty.Register<SelectionListControl, string>(
          nameof(Header), defaultValue: string.Empty);

  public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
      AvaloniaProperty.Register<SelectionListControl, IEnumerable?>(
          nameof(ItemsSource));

  public static readonly StyledProperty<object?> SelectedItemProperty =
      AvaloniaProperty.Register<SelectionListControl, object?>(
          nameof(SelectedItem),
          defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

  public static readonly StyledProperty<object?> LoadedItemProperty =
      AvaloniaProperty.Register<SelectionListControl, object?>(
          nameof(LoadedItem),
          defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

  public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
      AvaloniaProperty.Register<SelectionListControl, IDataTemplate?>(
          nameof(ItemTemplate));

  public static readonly StyledProperty<ICommand?> LoadCommandProperty =
      AvaloniaProperty.Register<SelectionListControl, ICommand?>(
          nameof(LoadCommand));

  public static readonly StyledProperty<ICommand?> UnloadCommandProperty =
      AvaloniaProperty.Register<SelectionListControl, ICommand?>(
          nameof(UnloadCommand));

  public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
      AvaloniaProperty.Register<SelectionListControl, ICommand?>(
          nameof(RefreshCommand));

  // ── CLR プロパティ ──────────────────────────────────────────────────────

  public string          Header       { get => GetValue(HeaderProperty);       set => SetValue(HeaderProperty, value); }
  public IEnumerable?    ItemsSource  { get => GetValue(ItemsSourceProperty);  set => SetValue(ItemsSourceProperty, value); }
  public object?         SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
  public object?         LoadedItem   { get => GetValue(LoadedItemProperty);   set => SetValue(LoadedItemProperty, value); }
  public IDataTemplate?  ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
  public ICommand?       LoadCommand  { get => GetValue(LoadCommandProperty);  set => SetValue(LoadCommandProperty, value); }
  public ICommand?       UnloadCommand{ get => GetValue(UnloadCommandProperty);set => SetValue(UnloadCommandProperty, value); }
  public ICommand?       RefreshCommand{get => GetValue(RefreshCommandProperty);set=> SetValue(RefreshCommandProperty, value); }

  // ── 초기화 ───────────────────────────────────────────────────────────

  public SelectionListControl()
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
    UnloadButton.Click           += OnUnloadClicked;
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    ItemListBox.SelectionChanged -= OnSelectionChanged;
    ItemListBox.DoubleTapped     -= OnDoubleTapped;
    LoadButton.Click             -= OnLoadClicked;
    UnloadButton.Click           -= OnUnloadClicked;
    base.OnDetachedFromVisualTree(e);
  }

  // ── StyledProperty 변경 → 내부 뷰 동기화 ─────────────────────────────

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
  {
    base.OnPropertyChanged(change);

    if (ItemListBox is null)
      return;

    if      (change.Property == HeaderProperty)        HeaderTextBlock.Text     = change.GetNewValue<string>();
    else if (change.Property == ItemsSourceProperty)   ItemListBox.ItemsSource  = change.GetNewValue<IEnumerable?>();
    else if (change.Property == SelectedItemProperty)  ApplySelectedState(change.NewValue);
    else if (change.Property == ItemTemplateProperty)  ItemListBox.ItemTemplate = change.GetNewValue<IDataTemplate?>();
    else if (change.Property == RefreshCommandProperty)RefreshButton.Command    = change.GetNewValue<ICommand?>();
    else if (change.Property == LoadedItemProperty)    ApplyLoadedState(change.NewValue is not null);
  }

  // ── 이벤트 핸들러 ────────────────────────────────────────────────────

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
      SetCurrentValue(SelectedItemProperty, ItemListBox.SelectedItem);

  private void OnDoubleTapped(object? sender, RoutedEventArgs e)
  {
    if (SelectedItem is not null)
      TriggerLoad();
  }

  private void OnLoadClicked(object? sender, RoutedEventArgs e) => TriggerLoad();

  private void OnUnloadClicked(object? sender, RoutedEventArgs e)
  {
    if (UnloadCommand?.CanExecute(null) == true)
      UnloadCommand.Execute(null);
    SetCurrentValue(LoadedItemProperty, null);
  }

  // ── 내부 로직 ─────────────────────────────────────────────────────────

  private void TriggerLoad()
  {
    if (SelectedItem is null)
      return;
    SetCurrentValue(LoadedItemProperty, SelectedItem);
    if (LoadCommand?.CanExecute(SelectedItem) == true)
      LoadCommand.Execute(SelectedItem);
  }

  private void ApplyAllToView()
  {
    HeaderTextBlock.Text     = Header;
    ItemListBox.ItemsSource  = ItemsSource;
    ItemListBox.ItemTemplate = ItemTemplate;
    RefreshButton.Command    = RefreshCommand;
    ApplySelectedState(SelectedItem);
    ApplyLoadedState(LoadedItem is not null);
  }

  private void ApplySelectedState(object? selectedItem)
  {
    ItemListBox.SelectedItem = selectedItem;
    LoadButton.IsEnabled     = selectedItem is not null;
  }

  private void ApplyLoadedState(bool isLoaded)
  {
    LoadButton.IsVisible     = !isLoaded;
    UnloadButton.IsVisible   =  isLoaded;
    ItemListBox.IsEnabled    = !isLoaded;
  }
}
