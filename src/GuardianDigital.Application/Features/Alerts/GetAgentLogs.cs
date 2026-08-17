using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GuardianDigital.Application.Features.Alerts;

public static class GetAgentLogs
{
    public static IResult Handle(IAgentLogService logService, int count = 50)
    {
        var logs = logService.GetLogs(count);
        return Results.Ok(logs);
    }
}
