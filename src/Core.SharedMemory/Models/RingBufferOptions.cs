namespace Core.SharedMemory.Models;

public class RingBufferOptions
{
    public string Name      { get; set; } = "fgs_ringbuffer";
    public int    SlotCount { get; set; } = 8;

    /// <summary>
    /// 슬롯당 최대 픽셀 데이터 크기 (bytes).
    /// 곱셈 표현식을 지원한다: e.g. "1024 * 1024 * 3 * 8"
    /// (Width * Height * MaxBytesPerPixel * 여유배수)
    /// </summary>
    public string SlotDataSize { get; set; } = "1024 * 1024 * 3 * 8";

    public int ResolvedSlotDataSize => ParseProduct(SlotDataSize);

    private static int ParseProduct(string expr) =>
        expr.Split('*')
            .Select(t => int.Parse(t.Trim()))
            .Aggregate(1, (acc, x) => acc * x);
}
