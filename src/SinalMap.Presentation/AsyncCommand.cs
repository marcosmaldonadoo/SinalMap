using System.Windows.Input;

namespace SinalMap.Presentation;

/// <summary>Comando aguardável que impede reentrada e encaminha falhas ao apresentador.</summary>
public sealed class AsyncCommand(Func<Task> execute, Action<Exception> onError, Func<bool>? canExecute = null) : ICommand
{
    public bool IsExecuting { get; private set; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !IsExecuting && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        IsExecuting = true;
        NotifyCanExecuteChanged();
        try { await execute(); }
        catch (Exception ex) { onError(ex); }
        finally
        {
            IsExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
