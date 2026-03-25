using Avalonia.Media.Imaging;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class OpticSettingViewModel : ViewModelBase
{
    private readonly ILogService  _log;
    private readonly IFrameSource _frameSource;

    public OpticSettings Settings { get; } = new();

    // [ObservableProperty] 대신 수동 정의 —
    // 동일 인스턴스 재할당 시 자동 알림이 누락되지 않도록 setter가 항상 OnPropertyChanged를 발생시킨다.
    private WriteableBitmap? _frameImage;
    public WriteableBitmap? FrameImage
    {
        get => _frameImage;
        private set
        {
            _frameImage = value;
            OnPropertyChanged();
        }
    }

    public OpticSettingViewModel(ILogService logService, IFrameSource frameSource)
    {
        _log         = logService;
        _frameSource = frameSource;

        // FrameSwapped는 UI 스레드에서 발생하므로 직접 Source를 교체해도 안전하다.
        _frameSource.FrameSwapped += (_, bitmap) => FrameImage = bitmap;
        _frameSource.Start();
    }
}
