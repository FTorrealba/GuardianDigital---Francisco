using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GuardianDigital.Application.Features.Alerts;

public static class DispatchEmergencyAction
{
    public static async Task<IResult> HandleAsync(
        Guid id,
        IEmergencyManagementService emergencyService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await emergencyService.DispatchActionsAsync(id, cancellationToken);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Emergency Dispatch Error");
        }
    }
}
