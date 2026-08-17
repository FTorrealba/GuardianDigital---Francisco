using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GuardianDigital.Application.Features.Onboarding;

public static class OnboardingFeature
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (IGuardianDbContext db) =>
            GetUsers.HandleAsync(db))
            .WithName("GetUsers")
            .WithTags("Onboarding");

        app.MapPost("/api/users", (CreateUser.CreateUserRequest request, IGuardianDbContext db) =>
            CreateUser.HandleAsync(request, db))
            .WithName("CreateUser")
            .WithTags("Onboarding");

        app.MapGet("/api/users/{id:guid}", (Guid id, IGuardianDbContext db) =>
            GetUserById.HandleAsync(id, db))
            .WithName("GetUserById")
            .WithTags("Onboarding");

        app.MapPut("/api/users/{id:guid}", (Guid id, UpdateUserProfile.UpdateUserRequest request, IGuardianDbContext db) =>
            UpdateUserProfile.HandleAsync(id, request, db))
            .WithName("UpdateUserProfile")
            .WithTags("Onboarding");

        app.MapDelete("/api/users/{id:guid}", (Guid id, IGuardianDbContext db) =>
            DeleteUser.HandleAsync(id, db))
            .WithName("DeleteUser")
            .WithTags("Onboarding");

        app.MapPut("/api/users/{id:guid}/contacts", (Guid id, UpdateEmergencyContacts.UpdateContactsRequest request, IGuardianDbContext db) =>
            UpdateEmergencyContacts.HandleAsync(id, request, db))
            .WithName("UpdateEmergencyContacts")
            .WithTags("Onboarding");
    }
}
