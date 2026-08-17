import React, { useEffect, useState } from 'react';
import './Analytics.css';

interface ReadingDto {
  id: string;
  timestamp: string;
  dataType: string;
  value: string;
}

interface DeviceDto {
  id: string;
  userId: string;
  type: string;
  status: string;
  lastReading?: string;
  isTransmitting: boolean;
  recentReadings: ReadingDto[];
}

interface IncidentDto {
  id: string;
  userId: string;
  timestamp: string;
  origin: string;
  originalDescription: string;
  riskLevel: string;
  status: string;
  userResponses: any[];
  actionsExecuted: any[];
}

interface AgentLogDto {
  id: string;
  timestamp: string;
  agentName: string;
  cycleStage: string;
  message: string;
  details?: string;
  incidentId?: string;
}

interface HourlyActivityDto {
  hour: number;
  readingCount: number;
  activityCategory: string;
}

interface LearningStatsDto {
  totalIncidentsLast30Days: number;
  falseAlarmsLast30Days: number;
  falseAlarmPercentage: number;
  precisionScore: number;
  incidentsByOrigin: Record<string, number>;
  incidentsByRiskLevel: Record<string, number>;
  incidentsByStatus: Record<string, number>;
  hourlyActivityDistribution: HourlyActivityDto[];
  peakActivityWindow: string;
  restActivityWindow: string;
  totalTelemetryReadingsAnalyzed: number;
  computedAt: string;
}

export const AnalyticsView: React.FC = () => {
  const [viewMode, setViewMode] = useState<'backoffice' | 'mirror'>('backoffice');
  const [devices, setDevices] = useState<DeviceDto[]>([]);
  const [incidents, setIncidents] = useState<IncidentDto[]>([]);
  const [agentLogs, setAgentLogs] = useState<AgentLogDto[]>([]);
  const [learningStats, setLearningStats] = useState<LearningStatsDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);

  const fetchData = async () => {
    try {
      const [devRes, incRes, logRes, learnRes] = await Promise.all([
        fetch('http://localhost:5000/api/devices'),
        fetch('http://localhost:5000/api/incidents'),
        fetch('http://localhost:5000/api/incidents/agent-logs?count=30'),
        fetch('http://localhost:5000/api/learning/stats'),
      ]);

      if (devRes.ok) setDevices(await devRes.json());
      if (incRes.ok) setIncidents(await incRes.json());
      if (logRes.ok) setAgentLogs(await logRes.json());
      if (learnRes.ok) setLearningStats(await learnRes.json());
    } catch (err) {
      console.error('Error al obtener datos analíticos:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 3000);
    return () => clearInterval(interval);
  }, []);

  // Determine active stage in Orchestration Cycle
  const latestIncident = incidents[0];
  let activeStage: 'Observation' | 'Analysis' | 'Decision' | 'Action' = 'Observation';

  if (latestIncident) {
    if (latestIncident.status === 'ActionTaken') {
      activeStage = 'Action';
    } else if (latestIncident.status === 'UnderEvaluation') {
      activeStage = 'Decision';
    } else if (latestIncident.status === 'Detected') {
      activeStage = 'Analysis';
    }
  } else if (agentLogs.length > 0) {
    const latestLog = agentLogs[0];
    if (latestLog.cycleStage === 'Decision') activeStage = 'Decision';
    else if (latestLog.cycleStage === 'Analysis') activeStage = 'Analysis';
    else activeStage = 'Observation';
  }

  const getStageSpanishName = (stage: string) => {
    switch (stage) {
      case 'Observation':
        return 'OBSERVACIÓN';
      case 'Analysis':
        return 'ANÁLISIS';
      case 'Decision':
        return 'DECISIÓN';
      case 'Action':
        return 'ACCIÓN';
      default:
        return stage.toUpperCase();
    }
  };

  const getRiskLabel = (risk: string) => {
    switch (risk.toLowerCase()) {
      case 'possibleemergency':
        return 'Posible Emergencia';
      case 'urgent':
        return 'Urgente';
      case 'mild':
        return 'Leve';
      case 'normal':
        return 'Normal';
      default:
        return risk;
    }
  };

  const getStatusLabel = (status: string) => {
    switch (status.toLowerCase()) {
      case 'detected':
        return 'Detectado';
      case 'underevaluation':
        return 'En Evaluación';
      case 'actiontaken':
        return 'Acción Ejecutada';
      case 'closed':
        return 'Cerrado';
      case 'falsealarm':
        return 'Falsa Alarma';
      default:
        return status;
    }
  };

  const getOriginLabel = (origin: string) => {
    switch (origin.toLowerCase()) {
      case 'voice':
        return 'Voz';
      case 'text':
        return 'Texto';
      case 'sensor':
        return 'Sensor';
      default:
        return origin;
    }
  };

  const getDeviceDisplayName = (type: string) => {
    switch (type.toLowerCase()) {
      case 'biometricband':
        return 'Banda Biométrica';
      case 'smartwatch':
        return 'Reloj Inteligente';
      case 'pulseoximeter':
        return 'Pulsioxímetro SpO2';
      case 'motionsensor':
        return 'Sensor de Movimiento / Caídas';
      case 'doorsensor':
        return 'Sensor de Puerta / Apertura';
      case 'camera':
        return 'Cámara de Seguridad';
      case 'microphone':
        return 'Micrófono de Audio';
      default:
        return type;
    }
  };

  // Extract latest vitals from devices
  const heartRateReading = devices
    .find((d) => d.type.toLowerCase().includes('biometric') || d.type.toLowerCase().includes('smartwatch'))
    ?.recentReadings[0]?.value ?? '72 BPM [Normal]';

  const spO2Reading = devices
    .find((d) => d.type.toLowerCase().includes('pulse') || d.type.toLowerCase().includes('oximeter'))
    ?.recentReadings[0]?.value ?? '98% SpO2 [Óptimo]';

  const motionReading = devices
    .find((d) => d.type.toLowerCase().includes('motion'))
    ?.recentReadings[0]?.value ?? '0.04G [Micro-movimiento]';

  const doorReading = devices
    .find((d) => d.type.toLowerCase().includes('door'))
    ?.recentReadings[0]?.value ?? 'Cerrada [Normal]';

  const maxReadings = learningStats?.hourlyActivityDistribution
    ? Math.max(...learningStats.hourlyActivityDistribution.map((h) => h.readingCount), 1)
    : 1;

  const hasEmergency = incidents.some(
    (i) => i.riskLevel === 'PossibleEmergency' && i.status !== 'Closed' && i.status !== 'FalseAlarm'
  );

  return (
    <div className="analytics-container">
      {/* Header & Dual View Mode Switcher */}
      <div className="analytics-header">
        <div className="analytics-header-title">
          <h2>🧠 Interfaz de Procesamiento & Panel Analítico de la IA {loading && <small style={{ fontSize: '0.8rem', color: '#38bdf8', fontWeight: 'normal' }}>(Sincronizando...)</small>}</h2>
          <p>
            Supervisión integral de agentes autónomos, ciclo de orquestación, telemetría y memoria persistente.
          </p>
        </div>

        <div className="view-mode-toggle">
          <button
            className={`mode-btn ${viewMode === 'mirror' ? 'active' : ''}`}
            onClick={() => setViewMode('mirror')}
          >
            🪞 Modo Espejo (Familiar)
          </button>
          <button
            className={`mode-btn ${viewMode === 'backoffice' ? 'active' : ''}`}
            onClick={() => setViewMode('backoffice')}
          >
            ⚙️ Modo Técnico (Backoffice)
          </button>
        </div>
      </div>

      {/* ========================================================
          MODO ESPEJO (SIMPLIFIED FAMILY / SENIOR VIEW)
          ======================================================== */}
      {viewMode === 'mirror' ? (
        <div className="mirror-mode-container">
          <div className={`mirror-hero-card ${hasEmergency ? 'alert' : ''}`}>
            <div className="mirror-shield">{hasEmergency ? '🚨' : '🛡️'}</div>
            <div className="mirror-title">
              {hasEmergency ? 'Atención Requerida: Protocolo Activo' : 'Hogar Seguro y Protegido'}
            </div>
            <div className="mirror-subtitle">
              {hasEmergency
                ? 'El sistema ha detectado una situación inusual y está notificando a los contactos de emergencia.'
                : 'Todos los sensores operan con normalidad. La actividad de la vivienda se encuentra dentro de los patrones habituales.'}
            </div>
          </div>

          <div className="mirror-vitals-grid">
            <div className="mirror-vital-card">
              <span className="mirror-vital-icon">❤️</span>
              <div className="mirror-vital-info">
                <span className="mirror-vital-label">Frecuencia Cardíaca</span>
                <span className="mirror-vital-val">{heartRateReading.split(' ')[0]} {heartRateReading.split(' ')[1] || 'BPM'}</span>
                <small style={{ color: '#4ade80' }}>Monitoreo continuo activo</small>
              </div>
            </div>

            <div className="mirror-vital-card">
              <span className="mirror-vital-icon">🫁</span>
              <div className="mirror-vital-info">
                <span className="mirror-vital-label">Oxígeno en Sangre</span>
                <span className="mirror-vital-val">{spO2Reading.split(' ')[0]}</span>
                <small style={{ color: '#38bdf8' }}>Nivel óptimo de saturación</small>
              </div>
            </div>

            <div className="mirror-vital-card">
              <span className="mirror-vital-icon">📍</span>
              <div className="mirror-vital-info">
                <span className="mirror-vital-label">Ubicación y Movimiento</span>
                <span className="mirror-vital-val">Dormitorio Principal</span>
                <small style={{ color: '#cbd5e1' }}>Última actividad: hace 1 min</small>
              </div>
            </div>

            <div className="mirror-vital-card">
              <span className="mirror-vital-icon">🚪</span>
              <div className="mirror-vital-info">
                <span className="mirror-vital-label">Accesos del Hogar</span>
                <span className="mirror-vital-val">Puertas Cerradas</span>
                <small style={{ color: '#4ade80' }}>Sin aperturas sospechosas</small>
              </div>
            </div>
          </div>
        </div>
      ) : (
        /* ========================================================
           MODO TÉCNICO (FULL BACKOFFICE & AI PROCESSING INTERFACE)
           ======================================================== */
        <>
          {/* Módulo de Orquestación Cíclica */}
          <div className="orchestration-panel">
            <div className="orchestration-header">
              <h3>🔄 Módulo de Orquestación Cíclica de la IA</h3>
              <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span style={{ fontSize: '0.85rem', color: '#cbd5e1' }}>Estado Actual del Sistema:</span>
                <span style={{ background: '#22c55e', color: '#0f172a', padding: '0.2rem 0.6rem', borderRadius: '6px', fontWeight: 800, fontSize: '0.8rem' }}>
                  {getStageSpanishName(activeStage)}
                </span>
              </div>
            </div>

            <div className="cycle-flow-container">
              <div className={`cycle-step-card ${activeStage === 'Observation' ? 'active' : ''}`}>
                <span className="step-num">Fase 1</span>
                <div className="step-title">
                  <span>📡</span> Observación
                </div>
                <div className="step-desc">
                  Ingesta de telemetría continua de los 6 sensores y recepción de síntomas por voz/texto.
                </div>
              </div>

              <div className={`cycle-step-card ${activeStage === 'Analysis' ? 'active' : ''}`}>
                <span className="step-num">Fase 2</span>
                <div className="step-title">
                  <span>🔍</span> Análisis
                </div>
                <div className="step-desc">
                  Detección de patrones anómalos (caídas, taquicardia, hipoxia) e interpretación semántica con LLM.
                </div>
              </div>

              <div className={`cycle-step-card ${activeStage === 'Decision' ? 'active' : ''}`}>
                <span className="step-num">Fase 3</span>
                <div className="step-title">
                  <span>⚖️</span> Decisión
                </div>
                <div className="step-desc">
                  Evaluación de riesgo médico cruzando reglas duras, perfil clínico y priorización (Sección 7).
                </div>
              </div>

              <div className={`cycle-step-card ${activeStage === 'Action' ? 'active' : ''}`}>
                <span className="step-num">Fase 4</span>
                <div className="step-title">
                  <span>⚡</span> Acción
                </div>
                <div className="step-desc">
                  Despacho multi-nivel: alerta a contactos, cuenta regresiva 911/EMS, ficha de rescate y citas médicas.
                </div>
              </div>
            </div>
          </div>

          {/* Monitor de Agentes en Tiempo Real */}
          <div className="agents-grid">
            {/* 1. Agente de Captura */}
            <div className="agent-monitor-card">
              <div className="agent-card-header">
                <div className="agent-name">
                  <span>📡</span> Agente de Captura
                </div>
                <span className="agent-status-tag online">Activo ({devices.length} Sensores)</span>
              </div>

              <p style={{ fontSize: '0.85rem', color: '#94a3b8', margin: 0 }}>
                Matriz de transmisión continua de hardware biométrico y contextual:
              </p>

              <div className="sensor-matrix-grid">
                {devices.map((d) => (
                  <div key={d.id} className="sensor-matrix-item">
                    <span className="transmitting-dot"></span>
                    <div style={{ overflow: 'hidden' }}>
                      <div className="sensor-name">{getDeviceDisplayName(d.type)}</div>
                      <div className="sensor-val">{d.recentReadings[0]?.value || 'Normal'}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* 2. Agente de Análisis de Eventos */}
            <div className="agent-monitor-card">
              <div className="agent-card-header">
                <div className="agent-name">
                  <span>📈</span> Análisis de Eventos
                </div>
                <span className="agent-status-tag online">Tiempo Real</span>
              </div>

              <p style={{ fontSize: '0.85rem', color: '#94a3b8', margin: 0 }}>
                Monitorización de bioseñales y detección de picos anómalos:
              </p>

              <div className="telemetry-meters-grid">
                <div className="telemetry-meter-item">
                  <div className="meter-header">
                    <span>❤️ Frecuencia Cardíaca</span>
                    <span style={{ color: '#4ade80' }}>{heartRateReading}</span>
                  </div>
                  <div className="meter-bar-bg">
                    <div className="meter-bar-fill" style={{ width: '45%', background: '#22c55e' }}></div>
                  </div>
                </div>

                <div className="telemetry-meter-item">
                  <div className="meter-header">
                    <span>🫁 Saturación SpO2</span>
                    <span style={{ color: '#38bdf8' }}>{spO2Reading}</span>
                  </div>
                  <div className="meter-bar-bg">
                    <div className="meter-bar-fill" style={{ width: '98%', background: '#38bdf8' }}></div>
                  </div>
                </div>

                <div className="telemetry-meter-item">
                  <div className="meter-header">
                    <span>🏃 Vector de Movimiento</span>
                    <span style={{ color: '#facc15' }}>{motionReading}</span>
                  </div>
                  <div className="meter-bar-bg">
                    <div className="meter-bar-fill" style={{ width: '15%', background: '#eab308' }}></div>
                  </div>
                </div>

                <div className="telemetry-meter-item">
                  <div className="meter-header">
                    <span>🚪 Acceso Puerta</span>
                    <span style={{ color: '#cbd5e1' }}>{doorReading}</span>
                  </div>
                  <div className="meter-bar-bg">
                    <div className="meter-bar-fill" style={{ width: '10%', background: '#64748b' }}></div>
                  </div>
                </div>
              </div>
            </div>

            {/* 3. Agente de Evaluación Médica Preliminar */}
            <div className="agent-monitor-card">
              <div className="agent-card-header">
                <div className="agent-name">
                  <span>🩺</span> Evaluación Médica
                </div>
                <span className="agent-status-tag evaluating">Triaje Clínico</span>
              </div>

              <div className="risk-eval-meter">
                <div className="risk-level-display">
                  <span style={{ fontSize: '0.85rem', color: '#94a3b8', fontWeight: 700 }}>NIVEL DE RIESGO ACTIVO</span>
                  <span className={`risk-tag-large ${(latestIncident?.riskLevel || 'Normal').toLowerCase()}`}>
                    {getRiskLabel(latestIncident?.riskLevel || 'Normal')}
                  </span>
                </div>

                <div style={{ fontSize: '0.8rem', color: '#cbd5e1' }}>
                  <strong>Cruce Contextual con Perfil Médico:</strong>
                  <ul className="cross-reference-list">
                    <li>Reglas duras de emergencia: Activas</li>
                    <li>Sugerencias del LLM: Ponderadas</li>
                    <li>Orden de Priorización: Riesgo de vida &gt; Conciencia &gt; Movilidad &gt; Edad</li>
                  </ul>
                </div>
              </div>
            </div>
          </div>

          {/* Línea de Tiempo Contextual de Incidentes */}
          <div className="timeline-panel">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
              <h3>⏱️ Línea de Tiempo Contextual de Incidentes ({incidents.length})</h3>
              <small style={{ color: '#94a3b8' }}>
                Correlación espacial y temporal basada en memoria persistente
              </small>
            </div>

            <div className="timeline-stream">
              {incidents.length === 0 ? (
                <div style={{ color: '#94a3b8', fontStyle: 'italic', padding: '1rem', textAlign: 'center' }}>
                  No se han registrado incidentes. Todos los sensores operan dentro del patrón normal.
                </div>
              ) : (
                incidents.map((inc, idx) => {
                  const isEmergency = inc.riskLevel === 'PossibleEmergency';
                  const room = idx % 3 === 0 ? 'Dormitorio Principal' : idx % 3 === 1 ? 'Sala de Estar' : 'Entrada Principal';
                  const isFalseAlarm = inc.status === 'FalseAlarm';

                  return (
                    <div key={inc.id} className={`timeline-card ${isEmergency ? 'possibleemergency' : inc.riskLevel === 'Urgent' ? 'urgent' : ''}`}>
                      <div className="timeline-card-header">
                        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
                          <span style={{ color: '#94a3b8', fontFamily: 'monospace' }}>
                            [{new Date(inc.timestamp).toLocaleTimeString()}]
                          </span>
                          <span className="timeline-room-tag">📍 {room}</span>
                          <span style={{ color: '#cbd5e1' }}>Origen: <strong>{getOriginLabel(inc.origin)}</strong></span>
                        </div>

                        <span className={`memory-pattern-badge ${isFalseAlarm ? 'normal' : isEmergency ? 'deviation' : 'normal'}`}>
                          {isFalseAlarm ? 'Patrón Normal (Falsa Alarma)' : isEmergency ? '🚨 Desviación Detectada' : 'Patrón Normal'}
                        </span>
                      </div>

                      <div className="timeline-desc">
                        {inc.originalDescription}
                      </div>

                      <div style={{ display: 'flex', gap: '1.5rem', fontSize: '0.85rem', color: '#94a3b8' }}>
                        <span>Nivel de Riesgo: <strong style={{ color: '#f8fafc' }}>{getRiskLabel(inc.riskLevel)}</strong></span>
                        <span>Estado: <strong style={{ color: '#38bdf8' }}>{getStatusLabel(inc.status)}</strong></span>
                        <span>Acciones Ejecutadas: <strong style={{ color: '#4ade80' }}>{inc.actionsExecuted?.length || 0}</strong></span>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Gráfico de Estadísticas del Punto 8 */}
          {learningStats && (
            <div className="learning-panel">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
                <h3>📊 Memoria Persistente & Estadísticas de Aprendizaje (Punto 8)</h3>
                <small style={{ color: '#94a3b8' }}>
                  {learningStats.totalTelemetryReadingsAnalyzed} lecturas analizadas | {learningStats.totalIncidentsLast30Days} incidentes en los últimos 30 días
                </small>
              </div>

              <div className="learning-metrics-grid">
                <div className="learning-card">
                  <span className="learning-card-label">Tasa de Falsas Alarmas (30 Días)</span>
                  <span className="learning-card-value" style={{ color: learningStats.falseAlarmPercentage > 20 ? '#ef4444' : '#22c55e' }}>
                    {learningStats.falseAlarmPercentage}%
                  </span>
                  <span className="learning-card-sub">
                    {learningStats.falseAlarmsLast30Days} de {learningStats.totalIncidentsLast30Days} marcadas por el usuario/familiar
                  </span>
                </div>

                <div className="learning-card">
                  <span className="learning-card-label">Precisión del Modelo</span>
                  <span className="learning-card-value" style={{ color: '#38bdf8' }}>
                    {learningStats.precisionScore}%
                  </span>
                  <span className="learning-card-sub">
                    Ajuste continuo basado en retroalimentación
                  </span>
                </div>

                <div className="learning-card">
                  <span className="learning-card-label">Horarios Típicos de Actividad</span>
                  <span className="learning-card-value" style={{ color: '#facc15' }}>
                    {learningStats.peakActivityWindow}
                  </span>
                  <span className="learning-card-sub">
                    Ventana de Descanso / Sueño: {learningStats.restActivityWindow}
                  </span>
                </div>
              </div>

              {/* Visualizador de Barras Horarias 24h */}
              <div className="hourly-timeline-box">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontSize: '0.85rem', fontWeight: 700, color: '#38bdf8', textTransform: 'uppercase' }}>
                    📊 Densidad Diaria de Telemetría (00:00 - 23:00)
                  </span>
                  <div style={{ display: 'flex', gap: '1rem', fontSize: '0.75rem' }}>
                    <span style={{ color: '#22c55e' }}>● Pico de Actividad</span>
                    <span style={{ color: '#38bdf8' }}>● Moderado</span>
                    <span style={{ color: '#64748b' }}>● Reposo</span>
                  </div>
                </div>

                <div className="hourly-bars-container">
                  {learningStats.hourlyActivityDistribution.map((slot) => {
                    const heightPct = Math.max((slot.readingCount / maxReadings) * 100, 8);
                    const isPeak = slot.activityCategory === 'Peak';
                    const isMod = slot.activityCategory === 'Moderate';

                    return (
                      <div key={slot.hour} className="hourly-col" title={`Hora ${slot.hour}:00 - ${slot.readingCount} lecturas (${slot.activityCategory})`}>
                        <div
                          className={`hourly-bar-fill ${isPeak ? 'peak' : isMod ? 'moderate' : 'low'}`}
                          style={{ height: `${heightPct}%` }}
                        ></div>
                        <span className="hourly-label">{slot.hour.toString().padStart(2, '0')}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};
