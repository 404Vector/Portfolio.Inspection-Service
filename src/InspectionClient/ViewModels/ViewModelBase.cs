using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    protected readonly ILogService _log;

    protected ViewModelBase(ILogService logService)
    {
        _log = logService;
    }

    protected void Execute(
        Action action,
        [CallerMemberName] string method = "")
    {
        _log.Debug(this, $"→ {method}");
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.Error(this, $"{method} failed: {ex.Message}");
            throw;
        }
        _log.Debug(this, $"← {method}");
    }

    protected async Task Execute(
        Func<Task> action,
        [CallerMemberName] string method = "")
    {
        _log.Debug(this, $"→ {method}");
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.Error(this, $"{method} failed: {ex.Message}");
            throw;
        }
        _log.Debug(this, $"← {method}");
    }
}
