using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace InspectionClient.Controls;

/// <summary>
/// Popup을 사용하지 않는 인라인 드롭다운 선택 컨트롤.
/// macOS에서 Popup 계열 컨트롤(ComboBox, DropDownButton) 렌더링 문제를 우회한다.
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

  // ── 항목 선택 ────────────────────────────────────────────────────────

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (ItemList.SelectedItem is null) return;

    SelectedItem            = ItemList.SelectedItem;
    DropDownPanel.IsVisible = false;
  }
}
