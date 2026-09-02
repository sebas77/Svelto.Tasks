//Note: Remember DateTime.Now is slow because it must do the conversion to local time
//Remember DateTime (then - now).Ticks is not precise (might change between machines). Only TimeSpan tick is machine agnostic

using System;

public static class TimeUtils
{
    /// <summary>
    /// Converts a TimeSpan to nanoseconds.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan to convert.</param>
    /// <returns>The equivalent time in nanoseconds.</returns>
    /// <remarks>1 tick = 100 nanoseconds</remarks>
    public static long ToNanoseconds(this TimeSpan timeSpan) => timeSpan.Ticks * 100;
}