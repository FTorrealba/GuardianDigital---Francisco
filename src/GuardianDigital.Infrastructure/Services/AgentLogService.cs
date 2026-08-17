using System.Collections.Concurrent;
using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

public class AgentLogService : IAgentLogService
{
    private readonly ConcurrentQueue<AgentLogDto> _logs = new();
    private readonly ILogger<AgentLogService> _logger;
    private const int MaxLogCapacity = 500;

    public AgentLogService(ILogger<AgentLogService> logger)
    {
        _logger = logger;
    }

    public void Log(string agentName, string cycleStage, string message, string? details = null, Guid? incidentId = null)
    {
        var entry = new AgentLogDto(
            Guid.NewGuid(),
            DateTime.UtcNow,
            agentName,
            cycleStage,
            message,
            details,
            incidentId
        );

        _logs.Enqueue(entry);

        while (_logs.Count > MaxLogCapacity)
        {
            _logs.TryDequeue(out _);
        }

        _logger.LogInformation("[{Agent}] [{Stage}] {Message} (Details: {Details})", agentName, cycleStage, message, details ?? "None");
    }

    public IReadOnlyList<AgentLogDto> GetLogs(int count = 50)
    {
        return _logs.Reverse().Take(count).ToList();
    }
}
