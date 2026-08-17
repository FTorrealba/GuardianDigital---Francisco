using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GuardianDigital.Application.Features.Alerts;

public static class RequestAppointment
{
    public record RequestAppointmentRequest(string? Notes = null);

    public static async Task<IResult> HandleAsync(
        Guid id,
        RequestAppointmentRequest request,
        IEmergencyManagementService emergencyService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await emergencyService.RequestMedicalAppointmentAsync(id, request.Notes, cancellationToken);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Appointment Booking Error");
        }
    }
}
