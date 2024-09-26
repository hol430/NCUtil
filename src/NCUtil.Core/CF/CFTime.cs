using System.Text.RegularExpressions;
using NCUtil.Core.Extensions;
using NCUtil.Core.Models;
using Attribute = NCUtil.Core.Models.Attribute;
using NodaTime;
using NCUtil.Core.Logging;
using System.Numerics;

namespace NCUtil.Core.CF;

/// <summary>
/// Utility functions for working with time data/variables.
/// </summary>
/// <remarks>
/// This probably doesn't belong in NCUtil.Core. Perhaps create another classlib
/// for everything related to the CF convention?
/// <remarks>
public static class CFTime
{
    /// <summary>
    /// Name of the calendar attribute.
    /// </summary>
    private const string attrCalendar = "calendar";

    /// <summary>
    /// Number of seconds in a minute.
    /// </summary>
    private const long secondsPerMinute = 60;

    /// <summary>
    /// Number of minutes in an hour.
    /// </summary>
    private const long minutesPerHour = 60;

    /// <summary>
    /// Number of hours in a day.
    /// </summary>
    private const long hoursPerDay = 24;

    /// <summary>
    /// Number of days in a month, when using a 360_day calendar.
    /// </summary>
    private const long daysPerMonth360 = 30;

    /// <summary>
    /// Number of days in a year, when using a 360_day calendar..
    /// </summary>
    private const long daysPerYear360 = 360;

    /// <summary>
    /// Number of days in a non-leap year.
    /// </summary>
    private const long daysPerStandardYear = 365;

    /// <summary>
    /// Number of days in a leap year.
    /// </summary>
    private const long daysPerLeapYear = 366;

    /// <summary>
    /// Number of seconds in an hour.
    /// </summary>
    private const long secondsPerHour = secondsPerMinute * minutesPerHour;

    /// <summary>
    /// Number of seconds in a day.
    /// </summary>
    private const long secondsPerDay = secondsPerHour * hoursPerDay;

    /// <summary>
    /// Number of nanoseconds in a second.
    /// </summary>
    private const long nanosecondsPerSecond = 1_000_000_000; // 1e9

    /// <summary>
    /// Number of nanoseconds per "tick" (used in DateTime class).
    /// </summary>
    private const long nanosecondsPerTick = 100;

    /// <summary>
    /// Number of nanoseconds in a minute.
    /// </summary>
    private const long nanosecondsPerMinute = nanosecondsPerSecond * secondsPerMinute;

    /// <summary>
    /// Number of nanoseconds in an hour.
    /// </summary>
    private const long nanosecondsPerHour = nanosecondsPerSecond * secondsPerHour;

    /// <summary>
    /// Number of nanoseconds in a day.
    /// </summary>
    private const long nanosecondsPerDay = nanosecondsPerSecond * secondsPerDay;

    /// <summary>
    /// Number of nanoseconds in a month, for a 360_day calendar.
    /// </summary>
    private const long nanosecondsPerMonth = nanosecondsPerDay * daysPerMonth360;

    /// <summary>
    /// Number of nanoseconds in a standard year.
    /// </summary>
    private const long nanosecondsPerStandardYear = nanosecondsPerDay * daysPerStandardYear;

    /// <summary>
    /// Number of nanoseconds in a year, when using a 360_day calendar.
    /// </summary>
    private const long nanosecondsPer360DayYear = nanosecondsPerDay * daysPerYear360;

    /// <summary>
    /// Number of nanoseconds in a leap year.
    /// </summary>
    private const long nanosecondsPerLeapYear = nanosecondsPerDay * daysPerLeapYear;

    /// <summary>
    /// Year in which the gregorian calendar was introduced.
    /// </summary>
    private const int firstGregorianYear = 1582;

    public static DateTime[] ReadDates(this NetCDFFile file)
    {
        return file.ReadDates(0, file.GetTimeDimension().Size);
    }

    public static DateTime ReadDate(this NetCDFFile file, int index)
    {
        return file.ReadDates(index, 1)[0];
    }

    public static DateTime[] ReadDates(this NetCDFFile file, int startIndex, int count)
    {
        Variable timeVariable = file.GetTimeVariable();
        Dimension timeDimension = file.GetTimeDimension();
        if (timeVariable.Dimensions.Count != 1)
            throw new InvalidOperationException($"Time is a variable of {timeVariable.Dimensions.Count} dimensions - should be 1 only");

        if ((startIndex + count) > timeDimension.Size)
            throw new InvalidOperationException($"Cannot read {count} time values starting at index {startIndex}: time variable contains only {timeDimension.Size} elements!");

        MutableRange range = new MutableRange();
        range.Start = startIndex;
        range.Count = count;
        Array timeData = timeVariable.Read([range]);
        // string units = nc.GetTimeVariable().ReadStringAttribute("units");
        TimeUnits units = ParseTimeUnits(timeVariable);
        CalendarType calendar = ParseCalendar(timeVariable);
        return timeData.Cast<long>().Select(t => GetDate(units, calendar, t)).ToArray();
    }

    /// <summary>
    /// Parse the contents of a calendar attribute.
    /// </summary>
    /// <param name="attribute">Value of a calendar attribute.</param>
    public static CalendarType ParseCalendar(string attribute)
    {
        switch (attribute)
        {
            case "standard":
            case "gregorian":
                return CalendarType.Standard;
            case "proleptic_gregorian":
                return CalendarType.ProlepticGregorian;
            case "julian":
                return CalendarType.Julian;
            case "noleap":
            case "365_day":
                return CalendarType.NoLeap;
            case "360_day":
                return CalendarType.EqualLength;
            case "none":
            case "":
                return CalendarType.None;
            default:
                throw new InvalidOperationException($"Unable to parse calendar type from attribute value: '{attribute}'");
        }
    }

    /// <summary>
    /// Parse the calendar attribute of this variable.
    /// </summary>
    /// <param name="variable">A time variable in a netcdf file.</param>
    public static CalendarType ParseCalendar(Variable variable)
    {
        if (!variable.IsTime())
            throw new InvalidOperationException($"Attempted to get calendar for non-time variable");

        string value = variable.ReadStringAttribute(attrCalendar);
        return ParseCalendar(value);
    }

    /// <summary>
    /// Parse the time units string of the specified variable.
    /// </summary>
    /// <param name="time">A time variable in a netcdf file.</param>
    public static TimeUnits ParseTimeUnits(Variable time)
	{
		string units = (string)time.GetAttribute("units").Value;

		string pattern = @"([^ ]+) since (.+)";
		Match match = Regex.Match(units, pattern);

		if (!match.Success)
			throw new InvalidOperationException($"Unable to parse time units: '{units}'");

		string deltaStr = match.Groups[1].Value;
		string baseTimeStr = match.Groups[2].Value;

		if (!Enum.TryParse(deltaStr, true, out TimeDelta delta))
			throw new InvalidOperationException($"Unable to parse time delta: '{deltaStr}'");

		if (!DateTime.TryParse(baseTimeStr, out DateTime baseTime))
			throw new InvalidOperationException($"Unable to parse datetime: '{baseTimeStr}'");

		return new TimeUnits(baseTime, delta, time.Name);
	}

    /// <summary>
    /// Convert the given numeric time value to a DateTime object, given the
    /// specified time units and calendar.
    /// </summary>
    /// <param name="units">Units of the time value.</param>
    /// <param name="calendar">Calendar of the time value.</param>
    /// <param name="timeValue">The time value.</param>
	public static DateTime GetDate(TimeUnits units, CalendarType calendar, long timeValue)
    {
        switch (calendar)
        {
            case CalendarType.Standard:
                return GetDateIso8601(units, timeValue);
            case CalendarType.ProlepticGregorian:
            case CalendarType.Julian:
                return GetDateStandard(timeValue, units, calendar);
            case CalendarType.EqualLength:
                return GetDate360Day(timeValue, units);
            case CalendarType.AllLeap:
                return GetDateAllLeap(timeValue, units);
            case CalendarType.NoLeap:
                return GetDateNoLeap(timeValue, units);
            default:
                throw new NotImplementedException($"Unsupported calendar: {calendar.ToEnumString()}");
        }
    }

    /// <summary>
    /// Convert the given numeric time value to a DateTime object, given the
    /// specified time units and a 365_day/no_leap calendar.
    /// </summary>
    /// <param name="timeValue">The time value.</param>
    /// <param name="units">Units of the time value.</param>
    private static DateTime GetDateNoLeap(double timeValue, TimeUnits units)
    {
        throw new NotImplementedException($"TBI: 365_day/no_leap calendar");
    }

    /// <summary>
    /// Convert the given numeric time value to a DateTime object, given the
    /// specified time units and a 366_day/all_leap calendar.
    /// </summary>
    /// <param name="timeValue">The time value.</param>
    /// <param name="units">Units of the time value.</param>
    private static DateTime GetDateAllLeap(double timeValue, TimeUnits units)
    {
        throw new NotImplementedException($"TBI: 366_day/all_leap calendar");
    }

    /// <summary>
    /// Convert the given numeric time value to a DateTime object, given the
    /// specified time units and a 360_day calendar.
    /// </summary>
    /// <param name="timeValue">The time value.</param>
    /// <param name="units">Units of the time value.</param>
    private static DateTime GetDate360Day(double timeValue, TimeUnits units)
    {
        if (units.BaseTime.Day > 30)
            throw new NotImplementedException($"Cannot use a calendar with 30-day months with a reference time starting on the 31st of a month.");
        throw new NotImplementedException($"TBI: 360_day calendar");
    }

    /// <summary>
    /// Get the conversion factor required to convert a value in the specified
    /// units and calendar into a value in nanoseconds.
    /// </summary>
    /// <param name="delta">The time units.</param>
    /// <param name="calendar">The calendar being used.</param>
    private static long GetConversionFactor(TimeDelta delta, CalendarType calendar)
    {
        if (delta == TimeDelta.Seconds)
            return nanosecondsPerSecond;
        if (delta == TimeDelta.Minutes)
            return nanosecondsPerMinute;
        if (delta == TimeDelta.Hours)
            return nanosecondsPerHour;
        if (delta == TimeDelta.Days)
            return nanosecondsPerDay;
        if (delta == TimeDelta.Months)
        {
            if (calendar != CalendarType.EqualLength)
                throw new InvalidOperationException($"Months as time units is only supported in 360_day calendar, but this calendar is: {calendar.ToEnumString()}");
            return nanosecondsPerMonth;
        }
        if (delta == TimeDelta.Years)
        {
            if (calendar == CalendarType.AllLeap)
                return nanosecondsPerLeapYear;
            if (calendar == CalendarType.NoLeap)
                return nanosecondsPerStandardYear;
            throw new InvalidOperationException($"Years as time units is only supported in 365_day/no_leap or 366_day/all_leap calendars, but this calendar is: {calendar.ToEnumString}");
        }
        throw new InvalidOperationException($"Invalid time units/calendar: {delta.ToEnumString()}/{calendar.ToEnumString()}");
    }

    /// <summary>
    /// Get a DateTime object for the specified time value using a "standard"
    /// (ie easily interpretable) calendar. This is done using the NodaTime
    /// library.
    /// </summary>
    /// <param name="offset">Time offset.</param>
    /// <param name="units">Units of the time offset.</param>
    /// <param name="calendar">The calendar system being used.</param>
    private static DateTime GetDateStandard(long offset, TimeUnits units, CalendarType calendar)
    {
        LocalDateTime date = AddTimeOffset(offset, units, calendar);
        return ToDateTime(date);
    }

    /// <summary>
    /// Convert a NodaTime LocalDateTime object to a System.DateTime.
    /// </summary>
    /// <param name="date">The date to be converted.</param>
    private static DateTime ToDateTime(LocalDateTime date)
    {
        return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second);
    }

    /// <summary>
    /// Convert the given CalendarType enum to a NodaTime CalendarSystem object.
    /// This will throw for most calendar types.
    /// </summary>
    /// <param name="calendar">The calendar type.</param>
    private static CalendarSystem GetCalendarSystem(CalendarType calendar)
    {
        switch (calendar)
        {
            case CalendarType.Standard:
                // CalendarSystem.Iso is proleptic, so it will be incorrect for
                // values before 1582-10-05.
                return CalendarSystem.Iso;
            case CalendarType.ProlepticGregorian:
                // This is a proleptic calendar, so it should be fine.
                return CalendarSystem.Gregorian;
            case CalendarType.Julian:
                return CalendarSystem.Julian;
            default:
                throw new NotImplementedException($"Unsupported calendar: {calendar.ToEnumString()}");
        }
    }

    /// <summary>
    /// Add the given time offset to the reference date, given a particular
    /// calendar and set of units for the time value.
    /// </summary>
    /// <remarks>
    /// This handles all standard calendars (proleptic gregorian, julian, and
    /// ISO-8601 (for dates>1582)) using NodaTime.
    /// </remarks>
    /// <param name="timeOffset">The time offset.</param>
    /// <param name="units">The time units.</param>
    /// <param name="calendar">The calendar system being used.</param>
    private static LocalDateTime AddTimeOffset(long timeOffset, TimeUnits units, CalendarType calendar)
    {
        CalendarSystem calendarSystem = GetCalendarSystem(calendar);
        LocalDateTime referenceDate = LocalDateTime.FromDateTime(units.BaseTime, calendarSystem);
        long factor = GetConversionFactor(units.Delta, calendar);

        // Convert time offset into nanoseconds.
        long offsetns = timeOffset * factor;
        return referenceDate.PlusNanoseconds(offsetns);
    }

    /// <summary>
    /// Convert a numeric value to a DateTime object, using the "standard" ISO
    /// 8601 calendar.
    /// </summary>
    /// <param name="units">Time units (e.g. 'days since 1970-01-01').</param>
    /// <param name="timeValue">The numeric to be interpreted.</param>
	private static DateTime GetDateIso8601(TimeUnits units, long timeValue)
	{
        if (units.BaseTime.Year > firstGregorianYear && timeValue > 0)
            // This should catch the vast majority of files.
            return GetDateStandard(timeValue, units, CalendarType.ProlepticGregorian);

        // We need to use the Julian calendar before 1582, then switch to
        // Gregorian. This is left as an exercise to the reader.

        throw new NotImplementedException($"Standard calendars with reference dates <{firstGregorianYear} not supported");

        // System.DateTime uses a proleptic gregorian calendar.
        // NodaTime uses a proleptic gregorian calendar.

		// switch (units.Delta)
		// {
		// 	case TimeDelta.Seconds:
		// 		return units.BaseTime.AddSeconds(timeValue);
		// 	case TimeDelta.Minutes:
		// 		return units.BaseTime.AddMinutes(timeValue);
		// 	case TimeDelta.Days:
		// 		return units.BaseTime.AddDays(timeValue);
		// 	case TimeDelta.Hours:
		// 		return units.BaseTime.AddHours(timeValue);
		// 	case TimeDelta.Months:
		// 		return units.BaseTime.AddMonths((int)timeValue); // + remainder
		// 	case TimeDelta.Years:
		// 		return units.BaseTime.AddYears((int)timeValue); // + remainder
		// 	default:
		// 		throw new InvalidOperationException($"Unknown time units: {units.Delta}");
		// }
	}
}
