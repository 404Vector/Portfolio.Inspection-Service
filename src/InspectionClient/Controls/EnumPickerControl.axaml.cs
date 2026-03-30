using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace InspectionClient.Controls;

/// <summary>
/// Popup을 사용하지 않는 인라인 드롭다운 선택 컨트롤.
/// macOS에서 Popup 계열 컨트롤(ComboBox, DropDownButton) 렌더링 문제를 우회한다.
///
/// 특징:
/// - Grid 레이아웃으로 DropDownPanel을 ToggleButton 아래에 배치 (ZIndex 겹침 방지)
/// - PointerPressed 이벤트로 항목 클릭 즉시 닫힘 (이미 선택된 항목도 처리)
/// - LostFocus로 외부 클릭 시 자동 닫힘
/// </summary>
public partial class EnumPickerControl : UserControl
{
  // ── Avalonia Properties ──────────────────────────────────────────────

  public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
      AvaloniaProperty.Register<EnumPickerControl, IEnumerable?>(nameof(ItemsSource));

  public static readonly StyledProperty<object?> SelectedItemProperty =
      AvaloniaProperty.Register<EnumPickerControl, object?>(
          nameof(SelectedItem),
          defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

  public IEnumerable? ItemsSource
  {
    get => GetValue(ItemsSourceProperty);
    set => SetValue(ItemsSourceProperty, value);
  }

  public object? SelectedItem
  {
    get => GetValue(SelectedItemProperty);
    set => SetValue(SelectedItemProperty, value);
  }

  // ── 생성자 ───────────────────────────────────────────────────────────

  public EnumPickerControl()
  {
    InitializeComponent();
  }

  // ── Property 변경 감지 ───────────────────────────────────────────────

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
  {
    base.OnPropertyChanged(change);

    if (change.Property == ItemsSourceProperty)
      ItemList.ItemsSource = change.NewValue as IEnumerable;

    if (change.Property == SelectedItemProperty)
    {
      SelectedLabel.Text         = change.NewValue?.ToString() ?? "";
      ItemList.SelectedItem      = change.NewValue;
    }
  }

  // ── 토글 ─────────────────────────────────────────────────────────────

  private void OnToggleClicked(object? sender, RoutedEventArgs e)
  {
    DropDownPanel.IsVisible = !DropDownPanel.IsVisible;

    if (DropDownPanel.IsVisible)
      ItemList.Focus();
  }

  // ── 항목 포인터 이벤트 ───────────────────────────────────────────────
  // SelectionChanged 대신 PointerPressed를 사용해서 항목 클릭 즉시 닫힘
  // (이미 선택된 항목을 다시 클릭해도 닫힘)

  private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    // ListBox 항목을 클릭한 경우만 처리
    var point = e.GetCurrentPoint(ItemList);
    if (!point.Properties.IsLeftButtonPressed) return;

    // 클릭한 항목 추출
    var listBox = (ListBox)sender!;
    var clickPos = e.GetPosition(ItemList);
    var hitItem = listBox.GetItemAtPoint(clickPos);

    if (hitItem?.Item is not null)
    {
      SelectedItem            = hitItem.Item;
      DropDownPanel.IsVisible = false;
      e.Handled               = true;
    }
  }

  // ── 항목 선택 (일관성 유지용, SelectionChanged는 키보드 네비게이션 등에 대비) ─

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (ItemList.SelectedItem is null) return;

    SelectedItem = ItemList.SelectedItem;
  }

  // ── LostFocus: 다른 영역 클릭 시 드롭다운 닫힘 ──────────────────────

  private void OnLostFocus(object? sender, RoutedEventArgs e)
  {
    DropDownPanel.IsVisible = false;
  }
}
