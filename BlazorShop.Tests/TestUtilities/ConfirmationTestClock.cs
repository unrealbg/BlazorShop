namespace BlazorShop.Tests.TestUtilities;

internal sealed class ConfirmationTestClock : TimeProvider
{
    private long ticks = DateTimeOffset.UtcNow.UtcTicks / 10 * 10; // PostgreSQL microsecond precision.
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    public void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
}
