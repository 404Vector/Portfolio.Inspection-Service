namespace Core.SharedMemory;

// enum으로 정의하지 않는 이유:
// Volatile.Write(ref int, int) 오버로드만 존재하며, 값 타입 제네릭 오버로드가 없다.
// SlotHeader.State(int)에 Volatile.Write로 쓸 때 enum이면 캐스트가 강제되어
// unsafe 코드 경계에서 불필요한 변환이 생긴다.
internal static class SlotState
{
    public const int Empty   = 0;  // 쓰기 가능 (또는 컨슈머가 읽은 후)
    public const int Writing = 1;  // 프로듀서가 기록 중
    public const int Ready   = 2;  // 컨슈머가 읽기 가능 (OverwriteOldest: 프로듀서도 덮어쓸 수 있음)
}
