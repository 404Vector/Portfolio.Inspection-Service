namespace Core.SharedMemory.Enums;

public enum ReadResult
{
    Ok,          // 픽셀 데이터 유효
    NotReady,    // 슬롯이 Ready 상태가 아님 (Empty 또는 Writing)
    Overwritten  // 읽는 도중 또는 읽기 전에 프로듀서가 슬롯을 덮어씀
}
