using System.Globalization;
using Relay.Utilities;

namespace Relay.Tests;

public sealed class NumericInputTests
{
    /// <summary>
    /// Runs <paramref name="body"/> with a specific UI culture so locale-dependent parsing
    /// can be asserted regardless of the machine running the tests.
    /// </summary>
    private static void InCulture(string cultureName, Action body)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void CommaDecimalIsParsedAsAFractionNotADigitGroup()
    {
        // Parsing "3,5" with NumberStyles.Any against the invariant culture yields 35,
        // because the comma is accepted as a thousands separator. That silently applied a
        // tenfold value to the user's model.
        InCulture("de-DE", () =>
        {
            Assert.True(NumericInput.TryParseUserNumber("3,5", out var value));
            Assert.Equal(3.5, value);

            Assert.True(NumericInput.TryParseUserNumber("1,25", out var quarter));
            Assert.Equal(1.25, quarter);
        });
    }

    [Fact]
    public void PeriodDecimalIsParsedInACommaDecimalCulture()
    {
        // Graph defaults are serialized invariant, so a de-DE user editing a field
        // pre-filled with "3.5" must still get three and a half.
        InCulture("de-DE", () =>
        {
            Assert.True(NumericInput.TryParseUserNumber("3.5", out var value));
            Assert.Equal(3.5, value);
        });
    }

    [Fact]
    public void PeriodDecimalIsParsedInAPeriodDecimalCulture()
    {
        InCulture("en-US", () =>
        {
            Assert.True(NumericInput.TryParseUserNumber("3.5", out var value));
            Assert.Equal(3.5, value);

            Assert.True(NumericInput.TryParseUserNumber("-12", out var negative));
            Assert.Equal(-12, negative);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    public void NonNumericTextIsRejected(string text)
    {
        Assert.False(NumericInput.TryParseUserNumber(text, out _));
    }

    [Fact]
    public void GraphValuesAreReadInvariantRegardlessOfCulture()
    {
        InCulture("de-DE", () =>
        {
            Assert.Equal(3.5, NumericInput.FromGraphValue("3.5"));
            Assert.Equal(7.0, NumericInput.FromGraphValue(7));
            Assert.Equal(2.5, NumericInput.FromGraphValue(2.5));
        });
    }

    [Fact]
    public void DisplayFormattingStaysInvariantSoValuesRoundTrip()
    {
        InCulture("de-DE", () =>
        {
            Assert.Equal("3.5", NumericInput.Format(3.5, isInteger: false));
            Assert.Equal("4", NumericInput.Format(3.7, isInteger: true));
        });
    }

    [Fact]
    public void FormattedValuesReparseToThemselves()
    {
        InCulture("de-DE", () =>
        {
            var formatted = NumericInput.Format(12.75, isInteger: false);
            Assert.True(NumericInput.TryParseUserNumber(formatted, out var reparsed));
            Assert.Equal(12.75, reparsed);
        });
    }
}
