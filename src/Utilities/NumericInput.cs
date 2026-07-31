using System.Globalization;

namespace Relay.Utilities
{
    /// <summary>
    /// Number parsing for the generated input dialog. Lives outside the WPF layer so the
    /// culture handling can be unit tested.
    /// </summary>
    internal static class NumericInput
    {
        /// <summary>
        /// Parses a number the user typed, accepting their own locale first and invariant
        /// form second.
        /// </summary>
        /// <remarks>
        /// <see cref="NumberStyles.Float"/> rather than <c>Any</c> is deliberate. <c>Any</c>
        /// permits group separators, and invariant parsing then reads the comma-decimal
        /// "3,5" as the digit group 35 — a silent tenfold error for every user in a
        /// comma-decimal locale.
        /// </remarks>
        internal static bool TryParseUserNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Reads a default value that came from the graph JSON, where numbers are always
        /// serialized invariant.
        /// </summary>
        internal static double FromGraphValue(object value)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is float f) return f;
            double.TryParse(value?.ToString() ?? "0",
                NumberStyles.Float, CultureInfo.InvariantCulture, out var result);
            return result;
        }

        /// <summary>
        /// Formats a value for display in the dialog. Invariant so the text written back
        /// into the graph JSON round-trips regardless of the user's locale.
        /// </summary>
        internal static string Format(double value, bool isInteger) =>
            isInteger
                ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
