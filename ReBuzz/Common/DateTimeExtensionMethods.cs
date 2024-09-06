using System;

namespace ReBuzz.Common
{

    /// <summary>
    /// Extension methods for accessing Microseconds and Nanoseconds of a
    /// DateTime object.
    /// </summary>
    public static class DateTimeExtensionMethods
    {
        /// <summary>
        /// The number of ticks per microsecond.
        /// </summary>
        public const int TicksPerMicrosecond = 10;
        /// <summary>
        /// The number of ticks per Nanosecond.
        /// </summary>
        public const int NanosecondsPerTick = 100;

        /// <summary>
        /// Gets the microsecond fraction of a DateTime.
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static int Microseconds(this DateTime self)
        {
            return (int)Math.Floor(
                (self.Ticks
                % TimeSpan.TicksPerMillisecond)
                / (double)TicksPerMicrosecond);
        }
        /// <summary>
        /// Gets the Nanosecond fraction of a DateTime.  Note that the DateTime
        /// object can only store nanoseconds at resolution of 100 nanoseconds.
        /// </summary>
        /// <param name="self">The DateTime object.</param>
        /// <returns>the number of Nanoseconds.</returns>
        public static int Nanoseconds(this DateTime self)
        {
            return (int)(self.Ticks % TimeSpan.TicksPerMillisecond % TicksPerMicrosecond)
                * NanosecondsPerTick;
        }
        /// <summary>
        /// Adds a number of microseconds to this DateTime object.
        /// </summary>
        /// <param name="self">The DateTime object.</param>
        /// <param name="microseconds">The number of milliseconds to add.</param>
        public static DateTime AddMicroseconds(this DateTime self, int microseconds)
        {
            return self.AddTicks(microseconds * TicksPerMicrosecond);
        }
        /// <summary>
        /// Adds a number of nanoseconds to this DateTime object.  Note: this
        /// object only stores nanoseconds of resolutions of 100 seconds.
        /// Any nanoseconds passed in lower than that will be rounded using
        /// the default rounding algorithm in Math.Round().
        /// </summary>
        /// <param name="self">The DateTime object.</param>
        /// <param name="nanoseconds">The number of nanoseconds to add.</param>
        public static DateTime AddNanoseconds(this DateTime self, int nanoseconds)
        {
            return self.AddTicks((int)Math.Round(nanoseconds / (double)NanosecondsPerTick));
        }
    }

}
