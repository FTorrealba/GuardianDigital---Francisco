using GuardianDigital.Domain.Exceptions;
using GuardianDigital.Domain.ValueObjects;
using Xunit;

namespace GuardianDigital.Tests.Domain;

public class BloodTypeTests
{
    [Theory]
    [InlineData("A+")]
    [InlineData("A-")]
    [InlineData("B+")]
    [InlineData("B-")]
    [InlineData("AB+")]
    [InlineData("AB-")]
    [InlineData("O+")]
    [InlineData("O-")]
    public void BloodType_ValidTypes_CreateSuccessfully(string input)
    {
        var bloodType = new BloodType(input);
        Assert.Equal(input.ToUpperInvariant(), bloodType.Value);
    }

    [Theory]
    [InlineData("C+")]
    [InlineData("XYZ")]
    [InlineData("")]
    public void BloodType_InvalidTypes_ThrowDomainException(string input)
    {
        Assert.Throws<DomainException>(() => new BloodType(input));
    }
}
