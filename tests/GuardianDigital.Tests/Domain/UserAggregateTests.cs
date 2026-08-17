using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.Exceptions;
using GuardianDigital.Domain.ValueObjects;
using Xunit;

namespace GuardianDigital.Tests.Domain;

public class UserAggregateTests
{
    private static BloodType ValidBloodType => new BloodType("O+");

    private static EmergencyContact CreateContact(string name) => new EmergencyContact
    {
        ContactName = name,
        Relationship = "Family",
        Phone = "+123456789",
        PreferredMethod = ContactPreferredMethod.Call
    };

    [Fact]
    public void CreateUser_WithLessThan3Contacts_ThrowsDomainException()
    {
        var contacts = new List<EmergencyContact>
        {
            CreateContact("Contact 1"),
            CreateContact("Contact 2")
        };

        var ex = Assert.Throws<DomainException>(() => new User(
            "Maria Garcia",
            "12345678A",
            new DateTime(1980, 1, 1),
            "Female",
            "+123456789",
            "Main Street 123",
            ValidBloodType,
            contacts
        ));

        Assert.Equal("A user must have at least 3 emergency contacts.", ex.Message);
    }

    [Fact]
    public void CreateUser_With3OrMoreContacts_Succeeds()
    {
        var contacts = new List<EmergencyContact>
        {
            CreateContact("Contact 1"),
            CreateContact("Contact 2"),
            CreateContact("Contact 3")
        };

        var user = new User(
            "Maria Garcia",
            "12345678A",
            new DateTime(1980, 1, 1),
            "Female",
            "+123456789",
            "Main Street 123",
            ValidBloodType,
            contacts
        );

        Assert.NotNull(user);
        Assert.Equal(3, user.EmergencyContacts.Count);
    }

    [Fact]
    public void RemoveEmergencyContact_ResultingInLessThan3_ThrowsDomainException()
    {
        var c1 = CreateContact("Contact 1");
        var c2 = CreateContact("Contact 2");
        var c3 = CreateContact("Contact 3");

        var user = new User(
            "Maria Garcia",
            "12345678A",
            new DateTime(1980, 1, 1),
            "Female",
            "+123456789",
            "Main Street 123",
            ValidBloodType,
            new[] { c1, c2, c3 }
        );

        var ex = Assert.Throws<DomainException>(() => user.RemoveEmergencyContact(c1));
        Assert.Contains("must have at least 3 emergency contacts", ex.Message);
    }
}
