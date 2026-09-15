using AwesomeAssertions;
using EvaGest.Services;
using Xunit;

namespace EvaGest.Tests.Calculations;

public class ContactValidatorTests
{
    [Theory]
    [InlineData("111111111")]
    [InlineData("111 111 111")]
    [InlineData("11 11 111 11")]
    [InlineData(" 111111111 ")]
    public void IsValidPhone_Nine_digits_regardless_of_spacing_Valid(string phone)
        => ContactValidator.IsValidPhone(phone).Should().BeTrue();

    [Theory]
    [InlineData("11111111")]     // 8 digits
    [InlineData("1111111111")]   // 10 digits
    [InlineData("+34611111111")] // country code makes it 11 digits
    [InlineData("11111111a")]    // letter
    [InlineData("")]
    public void IsValidPhone_Anything_other_than_nine_digits_Invalid(string phone)
        => ContactValidator.IsValidPhone(phone).Should().BeFalse();

    [Theory]
    [InlineData("joan@example.com")]
    [InlineData("joan.marti@example.co.uk")]
    public void IsValidEmail_Address_with_at_and_dot_Valid(string email)
        => ContactValidator.IsValidEmail(email).Should().BeTrue();

    [Theory]
    [InlineData("joanexample.com")]   // no @
    [InlineData("joan@examplecom")]   // no dot after @
    [InlineData("joan@.com")]         // nothing between @ and .
    [InlineData("joan@example.")]     // nothing after the dot
    [InlineData("joan @example.com")] // space
    [InlineData("")]
    public void IsValidEmail_Missing_at_or_dot_Invalid(string email)
        => ContactValidator.IsValidEmail(email).Should().BeFalse();
}
