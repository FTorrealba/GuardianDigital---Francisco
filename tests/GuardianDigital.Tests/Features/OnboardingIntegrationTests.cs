using GuardianDigital.Application.Features.Onboarding;
using GuardianDigital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class OnboardingIntegrationTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "OnboardingTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    [Fact]
    public async Task CreateUser_With2EmergencyContacts_ReturnsBadRequest400()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();

        var request = new CreateUser.CreateUserRequest(
            FullName: "Carlos Gomez",
            NationalId: "99887766",
            DateOfBirth: new DateTime(1965, 5, 12),
            Gender: "Male",
            PrimaryPhone: "+54911223344",
            Address: "Avenida Siempreviva 742",
            HealthInsurance: "OSDE",
            BloodType: "A+",
            MedicalProfile: new CreateUser.MedicalProfileRequest(
                MedicalHistory: "Hypertension",
                CurrentMedication: new List<string> { "Enalapril" },
                KnownAllergies: new List<string> { "Penicillin" },
                PreexistingConditions: new List<string> { "High Blood Pressure" }
            ),
            EmergencyContacts: new List<CreateUser.EmergencyContactRequest>
            {
                new("Ana Gomez", "Daughter", "+54911998877", "Call"),
                new("Luis Gomez", "Son", "+54911887766", "SMS")
                // Only 2 contacts!
            }
        );

        // Act
        var result = await CreateUser.HandleAsync(request, db);

        // Assert
        var badRequestResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_With3Contacts_PersistsSuccessfullyAndCanBeRetrieved()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();

        var request = new CreateUser.CreateUserRequest(
            FullName: "Beatriz Lopez",
            NationalId: "11223344",
            DateOfBirth: new DateTime(1958, 3, 20),
            Gender: "Female",
            PrimaryPhone: "+54911334455",
            Address: "Calle San Martin 456",
            HealthInsurance: "Swiss Medical",
            BloodType: "O-",
            MedicalProfile: new CreateUser.MedicalProfileRequest(
                MedicalHistory: "Type 2 Diabetes",
                CurrentMedication: new List<string> { "Metformin" },
                KnownAllergies: new List<string> { "Dust" },
                PreexistingConditions: new List<string> { "Diabetes" }
            ),
            EmergencyContacts: new List<CreateUser.EmergencyContactRequest>
            {
                new("Laura Lopez", "Daughter", "+54911991122", "Call"),
                new("Pedro Lopez", "Son", "+54911882233", "SMS"),
                new("Dr. Ramirez", "Physician", "+54911773344", "PushNotification")
            }
        );

        // Act - Create User
        var result = await CreateUser.HandleAsync(request, db);
        var createdResult = Assert.IsType<Created<CreateUser.UserResponseDto>>(result);
        Assert.NotNull(createdResult.Value);
        var createdUserId = createdResult.Value.Id;

        // Act - Retrieve User
        var getResult = await GetUserById.HandleAsync(createdUserId, db);
        var okResult = Assert.IsType<Ok<CreateUser.UserResponseDto>>(getResult);
        Assert.NotNull(okResult.Value);

        // Assert
        Assert.Equal("Beatriz Lopez", okResult.Value.FullName);
        Assert.Equal("11223344", okResult.Value.NationalId);
        Assert.Equal("O-", okResult.Value.BloodType);
        Assert.Equal(3, okResult.Value.EmergencyContacts.Count);
        Assert.NotNull(okResult.Value.MedicalProfile);
        Assert.Contains("Metformin", okResult.Value.MedicalProfile.CurrentMedication);
    }
}
