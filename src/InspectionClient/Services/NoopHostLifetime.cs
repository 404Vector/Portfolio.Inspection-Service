using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace InspectionClient.Services;

/// <summary>
/// Ctrl+C 처리를 Avalonia에게 위임하기 위해 ConsoleLifetime을 대체하는 빈 구현.
/// IHost는 IHostLifetime 없이는 시작할 수 없으므로 아무 동작도 하지 않는 구현으로 교체한다.
/// </summary>
internal sealed class NoopHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken)         => Task.CompletedTask;
}
