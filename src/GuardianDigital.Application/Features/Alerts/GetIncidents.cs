using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Alerts;

public static class GetIncidents
{
    public record IncidentDto(
        Guid Id,
        Guid UserId,
        DateTime Timestamp,
        string Origin,
        string OriginalDescription,
        string RiskLevel,
        string Status,
        List<UserResponseDto> UserResponses,
        List<ActionExecutedDto> ActionsExecuted
    );

    public record UserResponseDto(
        Guid Id,
        string Question,
        string Answer,
        DateTime Timestamp
    );

    public record ActionExecutedDto(
        Guid Id,
        string ActionType,
        DateTime Timestamp,
        string Result
    );

    public static async Task<IResult> HandleAsync(IGuardianDbContext db, Guid? userId = null)
    {
        var query = db.Incidents
            .Include(i => i.UserResponses)
            .Include(i => i.ActionsExecuted)
            .AsQueryable();

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(i => i.UserId == userId.Value);
        }

        var incidents = await query
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync();

        var dtos = incidents.Select(i => new IncidentDto(
            i.Id,
            i.UserId,
            i.Timestamp,
            i.Origin.ToString(),
            i.OriginalDescription,
            i.RiskLevel.ToString(),
            i.Status.ToString(),
            i.UserResponses.Select(r => new UserResponseDto(r.Id, r.Question, r.Answer, r.Timestamp)).ToList(),
            i.ActionsExecuted.Select(a => new ActionExecutedDto(a.Id, a.ActionType.ToString(), a.Timestamp, a.Result)).ToList()
        )).ToList();

        return Results.Ok(dtos);
    }
}
