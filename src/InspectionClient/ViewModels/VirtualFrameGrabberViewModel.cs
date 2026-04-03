using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

      // 3. Die Image 렌더링 → Wafer Image 합성
      var waferImage = ComposeWaferImage(waferInfo, dieMap, dieParams);
      _log.Info(this, $"Wafer image composed: {waferImage.Length:N0} bytes");

      // 4. wafer_image 전송 (streaming)
      var (imgOk, imgMsg) = await Controller.SetParameterWithStreamAsync(
          "wafer_image", waferImage);
      if (!imgOk)
      {
        _log.Warning(this, $"wafer_image upload failed: {imgMsg}");
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

  // ── Wafer Image 합성 ──────────────────────────────────────

  /// <summary>
  /// DieMap의 각 Die 위치에 Die Image를 타일링하여 Mono8 Wafer Image를 생성한다.
  /// 출력 해상도는 프레임 그래버의 width/height 파라미터를 사용한다.
  /// </summary>
  private byte[] ComposeWaferImage(
      WaferInfo waferInfo, DieMap dieMap, DieRenderingParameters dieParams)
  {
    // 출력 이미지 크기: 프레임 그래버 width/height 파라미터 사용
    int imageW = GetParameterInt("width", 1280);
    int imageH = GetParameterInt("height", 1024);

    // 웨이퍼 직경(µm) → 출력 이미지(px) 스케일
    double waferDiameterUm = 2 * waferInfo.RadiusUm;
    double scale = Math.Min(imageW / waferDiameterUm, imageH / waferDiameterUm);

    // Die 1개의 픽셀 크기 (웨이퍼 스케일 기준)
    int diePixelW = Math.Max(1, (int)(waferInfo.DieSize.WidthUm * scale));
    int diePixelH = Math.Max(1, (int)(waferInfo.DieSize.HeightUm * scale));

    // Die 1개를 BGRA 비트맵으로 렌더링 후, 출력 스케일에 맞춰 리샘플링
    var dieBitmapFull = RenderDieBitmap(dieParams);
    var dieBitmap = ResizeBgraBitmap(
        dieBitmapFull, dieParams.CanvasWidth, dieParams.CanvasHeight,
        diePixelW, diePixelH);

    // Wafer Image (Mono8)
    var wafer = new byte[imageW * imageH];

    double radiusUm = waferInfo.RadiusUm;

    foreach (var die in dieMap.Dies)
    {
      // 웨이퍼 좌표(µm) → 이미지 좌표(px)
      // 이미지 관례: 원점 = 좌상단, Y↓
      int destX = (int)((die.BottomLeft.Xum + radiusUm) * scale);
      int destY = (int)((radiusUm - die.BottomLeft.Yum - die.HeightUm) * scale);

      if (destX >= imageW || destY >= imageH) continue;

      int srcStartX = destX < 0 ? -destX : 0;
      int srcStartY = destY < 0 ? -destY : 0;
      int dx = Math.Max(0, destX);
      int dy = Math.Max(0, destY);
      int copyW = Math.Min(diePixelW - srcStartX, imageW - dx);
      int copyH = Math.Min(diePixelH - srcStartY, imageH - dy);

      if (copyW <= 0 || copyH <= 0) continue;

      for (int y = 0; y < copyH; y++)
      {
        for (int x = 0; x < copyW; x++)
        {
          int si = ((srcStartY + y) * diePixelW + (srcStartX + x)) * 4; // BGRA
          int di = (dy + y) * imageW + (dx + x);
          if (si + 2 < dieBitmap.Length)
          {
            // BGRA → Mono8 (luminance)
            wafer[di] = (byte)(0.114 * dieBitmap[si]
                             + 0.587 * dieBitmap[si + 1]
                             + 0.299 * dieBitmap[si + 2]);
          }
        }
      }
    }

    return wafer;
  }

  private int GetParameterInt(string key, int fallback)
  {
    var item = Parameters.FirstOrDefault(p => p.Key == key);
    return item?.CurrentValue switch
    {
      long l   => (int)l,
      int i    => i,
      decimal m => (int)m,
      double d => (int)d,
      _        => fallback,
    };
  }

  /// <summary>
  /// BGRA 비트맵을 Nearest-Neighbor로 리사이즈한다.
  /// </summary>
  private static byte[] ResizeBgraBitmap(
      byte[] src, int srcW, int srcH, int dstW, int dstH)
  {
    if (srcW == dstW && srcH == dstH) return src;

    var dst = new byte[dstW * dstH * 4];
    for (int y = 0; y < dstH; y++)
    {
      int sy = y * srcH / dstH;
      for (int x = 0; x < dstW; x++)
      {
        int sx = x * srcW / dstW;
        int si = (sy * srcW + sx) * 4;
        int di = (y * dstW + x) * 4;
        dst[di]     = src[si];
        dst[di + 1] = src[si + 1];
        dst[di + 2] = src[si + 2];
        dst[di + 3] = src[si + 3];
      }
    }
    return dst;
  }

  /// <summary>
  /// IDieImageRenderer로 Die 1개를 렌더링하여 BGRA byte[]를 반환한다.
  /// </summary>
  private byte[] RenderDieBitmap(DieRenderingParameters dieParams)
  {
    int w = dieParams.CanvasWidth;
    int h = dieParams.CanvasHeight;
    var pixelSize = new PixelSize(w, h);
    var dpi       = new Vector(96, 96);

    using var bitmap = new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque);

    using (var fb = bitmap.Lock())
    {
      // 배경 클리어
      unsafe
      {
        var ptr = (byte*)fb.Address;
        for (int i = 0; i < fb.RowBytes * h; i++) ptr[i] = 0;
      }
    }

    // DrawingContext를 통해 렌더링
    using var rt = new RenderTargetBitmap(pixelSize, dpi);
    using (var ctx = rt.CreateDrawingContext())
    {
      _dieRenderer.Render(ctx, dieParams);
    }

    // RenderTargetBitmap → byte[] 추출
    var result = new byte[w * h * 4];
    using var copyBitmap = new WriteableBitmap(pixelSize, dpi, Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque);
    rt.CopyPixels(new PixelRect(pixelSize), copyBitmap.Lock().Address, result.Length, w * 4);

    unsafe
    {
      using var fb = copyBitmap.Lock();
      var ptr = (byte*)fb.Address;
      for (int i = 0; i < result.Length && i < fb.RowBytes * h; i++)
        result[i] = ptr[i];
    }

    return result;
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
