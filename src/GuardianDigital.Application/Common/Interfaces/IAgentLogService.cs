using GuardianDigital.Application.Common.Models;

namespace GuardianDigital.Application.Common.Interfaces;

public interface IAgentLogService
{
    void Log(string agentName, string cycleStage, string message, string? details = null, Guid? incidentId = null);
    IReadOnlyList<AgentLogDto> GetLogs(int count = 50);
}
