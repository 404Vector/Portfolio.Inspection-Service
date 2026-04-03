using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class VirtualFrameGrabberViewModel : FrameGrabberViewModelBase
{
  private readonly IWaferInfoRepository _waferRepo;
  private readonly IRecipeRepository _recipeRepo;
  private readonly IDieRenderingParametersRepository _dieParamsRepo;
  private readonly IDieImageRenderer _dieRenderer;

  // ── Scan Source 설정 상태 ──────────────────────────────────

  [ObservableProperty]
  private bool _isScanSourceSettingOpen;

  [ObservableProperty]
  private bool _isGenerating;

  public ObservableCollection<WaferInfoRow> WaferList { get; } = [];
  public ObservableCollection<RecipeRow> RecipeList { get; } = [];

  [ObservableProperty]
  private WaferInfoRow? _selectedWafer;

  [ObservableProperty]
  private WaferInfoRow? _loadedWafer;

  [ObservableProperty]
  private RecipeRow? _selectedRecipe;

  [ObservableProperty]
  private RecipeRow? _loadedRecipe;

  public VirtualFrameGrabberViewModel(
      ILogService logService,
      IFrameGrabberController controller,
      IFrameSource frameSource,
      IServiceConnectionMonitor connectionMonitor,
      IWaferInfoRepository waferRepo,
      IRecipeRepository recipeRepo,
      IDieRenderingParametersRepository dieParamsRepo,
      IDieImageRenderer dieRenderer)
      : base(logService, controller, frameSource, connectionMonitor, CreateParameters())
  {
    _waferRepo     = waferRepo;
    _recipeRepo    = recipeRepo;
    _dieParamsRepo = dieParamsRepo;
    _dieRenderer   = dieRenderer;
  }

  private static GrabberParameterItem[] CreateParameters() =>
  [
    new() { Key = "frame_rate_hz",    DisplayName = "Frame Rate (Hz)",  ValueType = ParameterValueType.Double, MinValue = 1.0,  MaxValue = 1000.0, CurrentValue = 30.0,         OriginalValue = 30.0         },
    new() { Key = "width",            DisplayName = "Width (px)",       ValueType = ParameterValueType.Int64,  MinValue = 1L,   MaxValue = 16384L, CurrentValue = 1280L,        OriginalValue = 1280L        },
    new() { Key = "height",           DisplayName = "Height (px)",      ValueType = ParameterValueType.Int64,  MinValue = 1L,   MaxValue = 16384L, CurrentValue = 1024L,        OriginalValue = 1024L        },
    new() { Key = "pixel_format",     DisplayName = "Pixel Format",     ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "Mono8",      OriginalValue = "Mono8",      AllowedValues = ["Mono8", "Rgb8", "Bgr8"]              },
    new() { Key = "acquisition_mode", DisplayName = "Acquisition Mode", ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "Continuous", OriginalValue = "Continuous", AllowedValues = ["Continuous", "Triggered"]  },
    new() { Key = "source_mode",      DisplayName = "Source Mode",      ValueType = ParameterValueType.String, MinValue = null, MaxValue = null,   CurrentValue = "gradient",   OriginalValue = "gradient",   AllowedValues = ["gradient", "scan"]               },
  ];

  // ── Scan Source 설정 토글 ──────────────────────────────────

  [RelayCommand]
  private async Task ToggleScanSourceSetting()
  {
    IsScanSourceSettingOpen = !IsScanSourceSettingOpen;

    if (IsScanSourceSettingOpen)
    {
      await RefreshWafers();
      await RefreshRecipes();
    }
    else
    {
      LoadedWafer  = null;
      LoadedRecipe = null;
    }
  }

  [RelayCommand]
  private async Task RefreshWafers()
  {
    await Execute(async () =>
    {
      var items = await _waferRepo.ListAsync();
      WaferList.Clear();
      foreach (var item in items) WaferList.Add(item);
    });
  }

  [RelayCommand]
  private async Task RefreshRecipes()
  {
    await Execute(async () =>
    {
      var items = await _recipeRepo.ListAsync();
      RecipeList.Clear();
      foreach (var item in items) RecipeList.Add(item);
    });
  }

  // ── Generate: Die Image 렌더링 + ScanPlan 전송 ─────────────

  public bool CanGenerate => LoadedWafer is not null && LoadedRecipe is not null && !IsGenerating;

  partial void OnLoadedWaferChanged(WaferInfoRow? value) =>
      GenerateCommand.NotifyCanExecuteChanged();

  partial void OnLoadedRecipeChanged(RecipeRow? value) =>
      GenerateCommand.NotifyCanExecuteChanged();

  partial void OnIsGeneratingChanged(bool value) =>
      GenerateCommand.NotifyCanExecuteChanged();

  [RelayCommand(CanExecute = nameof(CanGenerate))]
  private async Task Generate()
  {
    if (LoadedWafer is null || LoadedRecipe is null) return;

    IsGenerating = true;

    await Execute(async () =>
    {
      var waferInfo = LoadedWafer.ToWaferInfo();
      var recipe    = LoadedRecipe.Recipe;

      // 1. DieMap 계산
      var dieMap = DieMap.From(waferInfo);

      // 2. DieRenderingParameters 조회 (없으면 기본값)
      DieRenderingParameters dieParams;
      if (LoadedWafer.DieParametersId is { } dieParamsId)
      {
        var row = await _dieParamsRepo.FindByIdAsync(dieParamsId);
        dieParams = row?.Parameters ?? CreateDefaultDieParams(waferInfo);
      }
      else
      {
        dieParams = CreateDefaultDieParams(waferInfo);
      }

      // 3. Die Image 렌더링 → PNG 인코딩 → 전송
      var dieImage = EncodeDieImagePng(dieParams);
      _log.Info(this, $"Die image encoded: {dieImage.Length:N0} bytes");

      // 4. die_image 전송 (streaming)
      var (imgOk, imgMsg) = await Controller.SetParameterWithStreamAsync(
          "die_image", dieImage);
      if (!imgOk)
      {
        _log.Warning(this, $"die_image upload failed: {imgMsg}");
        return;
      }

      // 5. ScanPlan 생성 및 전송 (streaming)
      var scanPlan  = ScanPlan.From(waferInfo, recipe.Fov, recipe.OverlapXum, recipe.OverlapYum);
      var planBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(scanPlan));
      var (planOk, planMsg) = await Controller.SetParameterWithStreamAsync(
          "scan_plan", planBytes);
      if (!planOk)
      {
        _log.Warning(this, $"scan_plan upload failed: {planMsg}");
        return;
      }

      _log.Info(this, $"Generate complete: {dieMap.DieCount} dies, {scanPlan.TotalShotCount} shots");
    });

    IsGenerating = false;
  }

  // ── Die Image 인코딩 ───────────────────────────────────────

  /// <summary>
  /// Die 1개를 렌더링하여 PNG로 인코딩한다. (1 px = 1 μm)
  /// 서버에서 이 이미지를 Die 물리 크기와 매핑하여 FOV 단위로 리샘플링한다.
  /// </summary>
  private byte[] EncodeDieImagePng(DieRenderingParameters dieParams)
  {
    int w = dieParams.CanvasWidth;
    int h = dieParams.CanvasHeight;
    var pixelSize = new PixelSize(w, h);
    var dpi       = new Vector(96, 96);

    using var rt = new RenderTargetBitmap(pixelSize, dpi);
    using (var ctx = rt.CreateDrawingContext())
    {
      _dieRenderer.Render(ctx, dieParams);
    }

    using var ms = new MemoryStream();
    rt.Save(ms);
    return ms.ToArray();
  }

  private static DieRenderingParameters CreateDefaultDieParams(WaferInfo wafer) => new()
  {
    CanvasWidth  = Math.Clamp((int)wafer.DieSize.WidthUm,
        DieRenderingParameters.Limits.CanvasWidthMin,
        DieRenderingParameters.Limits.CanvasWidthMax),
    CanvasHeight = Math.Clamp((int)wafer.DieSize.HeightUm,
        DieRenderingParameters.Limits.CanvasHeightMin,
        DieRenderingParameters.Limits.CanvasHeightMax),
  };
}
