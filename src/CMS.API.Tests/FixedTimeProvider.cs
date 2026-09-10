namespace CMS.API.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> pinned to one instant, so the 24-hour token lifetime can be
/// asserted exactly instead of within a tolerance.
/// </summary>
public class FixedTimeProvider : TimeProvider
{
    public FixedTimeProvider(DateTimeOffset now) => Now = now;

    /// <summary>The instant every call returns. Settable so a test can advance the clock.</summary>
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;
}
