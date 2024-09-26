namespace NCUtil.Core.CF;

/// <summary>
/// Encapsulates all information in a CF time specification.
/// </summary>
/// <remarks>
/// An example specification is "hours since 1900-01-01".
/// </remarks>
public class TimeUnits
{
    public DateTime BaseTime { get; private init; }
    public TimeDelta Delta { get; private init; }
    public string Name { get; private init; }
    public TimeUnits(DateTime baseTime, TimeDelta delta, string name)
    {
        BaseTime = baseTime;
        Delta = delta;
        Name = name;
    }
}
