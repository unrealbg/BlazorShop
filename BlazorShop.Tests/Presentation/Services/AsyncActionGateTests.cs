namespace BlazorShop.Tests.Presentation.Services
{
    using BlazorShop.Web.Services;

    using Xunit;

    public class AsyncActionGateTests
    {
        [Fact]
        public async Task RunAsync_PreventsDuplicateExecution()
        {
            var gate = new AsyncActionGate();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var executionCount = 0;

            var firstRun = gate.RunAsync(async () =>
            {
                executionCount++;
                entered.SetResult();
                await release.Task;
            });

            await entered.Task;
            var secondRunExecuted = await gate.RunAsync(() =>
            {
                executionCount++;
                return Task.CompletedTask;
            });

            Assert.False(secondRunExecuted);
            Assert.True(gate.IsRunning);
            Assert.Equal(1, executionCount);

            release.SetResult();
            var firstRunExecuted = await firstRun;

            Assert.True(firstRunExecuted);
            Assert.False(gate.IsRunning);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task RunAsync_ClearsBusyStateAfterFailure()
        {
            var gate = new AsyncActionGate();

            await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync(() => throw new InvalidOperationException("boom")));

            Assert.False(gate.IsRunning);
        }
    }
}