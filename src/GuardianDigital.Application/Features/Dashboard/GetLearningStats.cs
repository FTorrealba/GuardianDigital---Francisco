using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GuardianDigital.Application.Features.Dashboard;

public static class GetLearningStats
{
    public static async Task<IResult> HandleAsync(
        Guid? userId,
        ILearningStatsService learningStatsService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await learningStatsService.GetLearningStatsAsync(userId, cancellationToken);
            return Results.Ok(stats);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Learning Stats Error");
        }
    }
}
