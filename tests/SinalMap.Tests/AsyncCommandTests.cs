using SinalMap.Presentation;

namespace SinalMap.Tests;

public sealed class AsyncCommandTests
{
    [Fact]
    public async Task Reentry_is_rejected_until_the_first_execution_finishes()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var command = new AsyncCommand(async () => { calls++; await release.Task; }, _ => Assert.Fail("Falha inesperada."));
        var first = command.ExecuteAsync();
        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.Equal(1, calls);
        release.SetResult();
        await first;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Failure_is_reported_and_does_not_leave_the_command_locked()
    {
        Exception? reported = null;
        var exception = new IOException("synthetic");
        var command = new AsyncCommand(() => Task.FromException(exception), e => reported = e);
        await command.ExecuteAsync();
        Assert.Same(exception, reported);
        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Disabled_command_does_not_invoke_the_action()
    {
        var calls = 0;
        var command = new AsyncCommand(() => { calls++; return Task.CompletedTask; }, _ => { }, () => false);
        await command.ExecuteAsync();
        Assert.Equal(0, calls);
    }
}
