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
  // PointerPressed를 사용해서 항목 클릭 즉시 닫힘 (선택 여부와 무관)

  private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    // 좌클릭만 처리
    var point = e.GetCurrentPoint(ItemList);
    if (!point.Properties.IsLeftButtonPressed) return;

    // 클릭 위치에서 ListBoxItem을 찾음
    var clickPos = e.GetPosition(ItemList);
    var hitControl = ItemList.InputHitTest(clickPos);

    // 클릭한 컨트롤이 ListBoxItem 또는 그 자식이라면, 해당 항목을 선택
    var listBoxItem = FindAncestorOfType<ListBoxItem>(hitControl);
    if (listBoxItem?.DataContext is not null)
    {
      SelectedItem            = listBoxItem.DataContext;
      DropDownPanel.IsVisible = false;
      e.Handled               = true;
    }
  }

  /// <summary>
  /// 시각적 트리를 따라 부모로 이동하며 지정된 타입의 첫 번째 조상을 찾는다.
  /// </summary>
  private static T? FindAncestorOfType<T>(Control? control) where T : class
  {
    var current = control;
    while (current is not null)
    {
      if (current is T ancestor) return ancestor;
      current = current.Parent;
    }
    return null;
  }

  // ── 항목 선택 (키보드 네비게이션에서 Enter로 선택한 경우 패널 닫힘) ─────

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (ItemList.SelectedItem is null) return;

    SelectedItem = ItemList.SelectedItem;
    // 포커스가 있으면 (키보드 네비게이션) 패널 닫힘
    if (ItemList.IsFocused)
    {
      DropDownPanel.IsVisible = false;
    }
  }

  // ── LostFocus: EnumPickerControl 외부로 포커스 이동 시 패널 닫힘 ──────
  // IsKeyboardFocusWithin으로 전체 컨트롤 트리 내 포커스 상태를 확인

  private void OnLostFocus(object? sender, RoutedEventArgs e)
  {
    // 포커스가 이 컨트롤의 자식에서 완전히 떠났을 때만 패널을 닫음
    if (!IsKeyboardFocusWithin)
    {
      DropDownPanel.IsVisible = false;
    }
  }
}
