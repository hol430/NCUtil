namespace NCUtil.Core.Models;

/// <summary>
/// Calendars allowed by the CF conventions.
/// </summary>
public enum Calendar
{
    /// <summary>
    /// Mixed Gregorian/Julian calendar as defined by UDUNITS. This is the
    /// default. A deprecated alternative name for this calendar is gregorian.
    /// In this calendar, date/times after (and including) 1582-10-15 0:0:0 are
    /// in the Gregorian calendar, in which a year is a leap year if either (i)
    /// it is divisible by 4 but not by 100 or (ii) it is divisible by 400.
    /// Date/times before (and excluding) 1582-10-5 0:0:0 are in the Julian
    /// calendar. Year 1 AD or CE in the standard calendar is also year 1 of the
    /// julian calendar. In the standard calendar, 1582-10-15 0:0:0 is exactly 1
    /// day later than 1582-10-4 0:0:0 and the intervening dates are undefined.
    /// Therefore it is recommended that date/times in the range from (and
    /// including) 1582-10-5 0:0:0 until (but excluding) 1582-10-15 0:0:0 should
    /// not be used as reference in units, and that a time coordinate variable
    /// should not include any date/times in this range, because their
    /// interpretation is unclear. It is also recommended that a reference
    /// date/time before the discontinuity should not be used for date/times
    /// after the discontinuity, and vice-versa.
    /// </summary>
    Standard,

    /// <summary>
    /// A calendar with the Gregorian rules for leap-years extended to dates
    /// before 1582-10-15. All dates consistent with these rules are allowed,
    /// both before and after 1582-10-15 0:0:0.
    /// </summary>
    ProlepticGregorian,

    /// <summary>
    /// Julian calendar, in which a year is a leap year if it is divisible by 4,
    /// even if it is also divisible by 100.
    /// </summary>
    Julian,

    /// <summary>
    /// A calendar with no leap years, i.e., all years are 365 days long.
    /// </summary>
    NoLeap,

    /// <summary>
    /// A calendar in which every year is a leap year, i.e., all years are 366
    /// days long.
    /// </summary>
    AllLeap,

    /// <summary>
    /// A calendar in which all years are 360 days, and divided into 30 day
    /// months.
    /// </summary>
    EqualLength,

    /// <summary>
    /// No calendar.
    /// </summary>
    None,
}
