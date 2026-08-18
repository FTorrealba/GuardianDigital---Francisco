import React, { useEffect, useState } from 'react';
import './Alerts.css';
import { API_BASE_URL } from '../../config/api';

interface IncidentDto {
  id: string;
  userId: string;
  timestamp: string;
  origin: string;
  originalDescription: string;
  riskLevel: string;
  status: string;
  userResponses: any[];
  actionsExecuted: ActionExecutedDto[];
}

interface ActionExecutedDto {
  id: string;
  actionType: string;
  timestamp: string;
  result: string;
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

interface RescueSheetData {
  incidentId: string;
  incidentOrigin: string;
  incidentRiskLevel: string;
  incidentDescription: string;
  incidentTimestamp: string;
  patientId: string;
  patientFullName: string;
  nationalId: string;
  age: number;
  gender: string;
  primaryPhone: string;
  address: string;
  healthInsurance: string;
  bloodType: string;
  knownAllergies: string[];
  preexistingConditions: string[];
  currentMedication: string[];
  medicalHistory: string;
  emergencyContacts: {
    contactName: string;
    relationship: string;
    phone: string;
    preferredMethod: string;
  }[];
  generatedAt: string;
}

interface AlertsViewProps {
  activeUserId?: string;
  activeUserName?: string;
}

export const AlertsView: React.FC<AlertsViewProps> = ({ activeUserId, activeUserName }) => {
  const [incidents, setIncidents] = useState<IncidentDto[]>([]);
  const [agentLogs, setAgentLogs] = useState<AgentLogDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [evaluatingId, setEvaluatingId] = useState<string | null>(null);
  const [dispatchingId, setDispatchingId] = useState<string | null>(null);
  const [activeRescueSheet, setActiveRescueSheet] = useState<RescueSheetData | null>(null);
  const [loadingSheet, setLoadingSheet] = useState<boolean>(false);
  const [filterByUser, setFilterByUser] = useState<boolean>(false);

  const fetchData = async () => {
    try {
      const incUrl = filterByUser && activeUserId
        ? `${API_BASE_URL}/api/incidents?userId=${activeUserId}`
        : `${API_BASE_URL}/api/incidents`;
      const [incRes, logRes] = await Promise.all([
        fetch(incUrl),
        fetch(`${API_BASE_URL}/api/incidents/agent-logs?count=40`),
      ]);

      if (incRes.ok) {
        const data = await incRes.json();
        setIncidents(data);
      }

      if (logRes.ok) {
        const logData = await logRes.json();
        setAgentLogs(logData);
      }
    } catch (err) {
      console.error('Error al consultar incidentes o registros:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleEvaluateStatus = async (id: string, newStatus: string) => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/evaluate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newStatus, note: 'Evaluación manual desde el panel' }),
      });

      if (res.ok) {
        await fetchData();
      }
    } catch (err) {
      console.error('La evaluación falló:', err);
    }
  };

  const handleMedicalEvaluation = async (id: string) => {
    setEvaluatingId(id);
    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/evaluate-medical`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });

      if (res.ok) {
        await fetchData();
      }
    } catch (err) {
      console.error('La evaluación médica falló:', err);
    } finally {
      setEvaluatingId(null);
    }
  };

  const handleDispatchActions = async (id: string) => {
    setDispatchingId(id);
    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/dispatch-actions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });

      if (res.ok) {
        await fetchData();
      }
    } catch (err) {
      console.error('El despacho de acciones falló:', err);
    } finally {
      setDispatchingId(null);
    }
  };

  const handleRequestAppointment = async (id: string) => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/request-appointment`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ notes: 'Turno médico programado desde el panel de incidentes' }),
      });

      if (res.ok) {
        await fetchData();
      }
    } catch (err) {
      console.error('La solicitud de turno falló:', err);
    }
  };

  const handleMarkFalseAlarm = async (id: string) => {
    const reason = window.prompt(
      'Ingrese el motivo de la Falsa Alarma (ej. "Sensor caído accidentalmente", "Prueba de usuario", "Actividad física normal"):',
      'Usuario o familiar confirmó falsa alarma'
    );
    if (reason === null) return;

    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/false-alarm`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason }),
      });

      if (res.ok) {
        await fetchData();
      }
    } catch (err) {
      console.error('Error al marcar falsa alarma:', err);
    }
  };

  const handleViewRescueSheet = async (id: string) => {
    setLoadingSheet(true);
    try {
      const res = await fetch(`${API_BASE_URL}/api/incidents/${id}/rescue-sheet`);
      if (res.ok) {
        const sheetData: RescueSheetData = await res.json();
        setActiveRescueSheet(sheetData);
      }
    } catch (err) {
      console.error('Error al obtener la ficha de rescate:', err);
    } finally {
      setLoadingSheet(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 4000);
    return () => clearInterval(interval);
  }, [filterByUser, activeUserId]);

  const getRiskLabel = (risk: string) => {
    switch (risk.toLowerCase()) {
      case 'possibleemergency':
        return 'Posible Emergencia';
      case 'urgent':
        return 'Urgente';
      case 'mild':
        return 'Leve';
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

  return (
    <div className="alerts-container">
      <div className="alerts-header">
        <div>
          <h2>🚨 Monitor de Incidentes, Triaje & Gestión de Emergencias</h2>
          <p style={{ color: '#cbd5e1', margin: '0.25rem 0 0 0' }}>
            Ciclo multi-agente: Observación → Triaje Médico con IA → Despacho de Acciones & Protocolo 911.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
          {activeUserName && (
            <div style={{ display: 'flex', background: '#0f172a', border: '1px solid #334155', borderRadius: '10px', padding: '3px' }}>
              <button
                style={{
                  background: !filterByUser ? 'rgba(56, 189, 248, 0.25)' : 'transparent',
                  color: !filterByUser ? '#38bdf8' : '#94a3b8',
                  border: 'none',
                  padding: '0.35rem 0.75rem',
                  borderRadius: '7px',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
                onClick={() => setFilterByUser(false)}
              >
                🌐 Todos
              </button>
              <button
                style={{
                  background: filterByUser ? 'rgba(56, 189, 248, 0.25)' : 'transparent',
                  color: filterByUser ? '#38bdf8' : '#94a3b8',
                  border: 'none',
                  padding: '0.35rem 0.75rem',
                  borderRadius: '7px',
                  fontWeight: 700,
                  fontSize: '0.85rem',
                  cursor: 'pointer'
                }}
                onClick={() => setFilterByUser(true)}
              >
                👤 {activeUserName}
              </button>
            </div>
          )}
          <div style={{ background: 'rgba(239, 68, 68, 0.2)', border: '1px solid #ef4444', color: '#fca5a5', padding: '0.5rem 1rem', borderRadius: '12px', fontWeight: 'bold' }}>
            {incidents.length} Incidente(s)
          </div>
        </div>
      </div>

      {/* Realtime Agent Execution Cycle Stream */}
      <div className="agent-logs-section">
        <h3>⚡ Flujo de Ejecución Multi-Agente en Tiempo Real</h3>
        <div className="log-stream">
          {agentLogs.length === 0 ? (
            <div style={{ color: '#94a3b8', fontStyle: 'italic', padding: '0.5rem' }}>
              Esperando eventos del ciclo de agentes...
            </div>
          ) : (
            agentLogs.map((log) => (
              <div key={log.id} className={`log-entry ${log.cycleStage.toLowerCase()}`}>
                <span className="log-time">[{new Date(log.timestamp).toLocaleTimeString()}]</span>
                <span className="log-agent">[{log.agentName}:{log.cycleStage}]</span>
                <span>{log.message}</span>
                {log.details && (
                  <span style={{ color: '#94a3b8', fontSize: '0.85rem', display: 'block', marginTop: '0.2rem' }}>
                    ↪ {log.details}
                  </span>
                )}
              </div>
            ))
          )}
        </div>
      </div>

      {/* Detected Incidents Grid */}
      <div>
        <h3 style={{ color: '#f8fafc', fontSize: '1.4rem', marginBottom: '1rem' }}>
          Historial de Incidentes ({incidents.length})
        </h3>

        {loading && incidents.length === 0 ? (
          <p style={{ color: '#94a3b8' }}>Cargando flujo de incidentes...</p>
        ) : incidents.length === 0 ? (
          <div style={{ background: '#1e293b', padding: '2.5rem', borderRadius: '14px', textAlign: 'center', border: '1px solid #334155' }}>
            <span style={{ fontSize: '2.5rem' }}>🛡️</span>
            <p style={{ fontSize: '1.2rem', color: '#cbd5e1', margin: '0.5rem 0 0 0' }}>
              No se detectan incidentes activos. Todos los sensores y telemetría operan dentro de parámetros seguros.
            </p>
            <small style={{ color: '#94a3b8' }}>
              Consejo: Utilice la pestaña Copiloto de Voz/Texto o el Simulador de Sensores para generar un incidente de prueba.
            </small>
          </div>
        ) : (
          <div className="incidents-grid">
            {incidents.map((inc) => {
              const isEmergency = inc.riskLevel.toLowerCase().includes('emergency');
              const isUrgent = inc.riskLevel.toLowerCase().includes('urgent');
              const isUnderEval = inc.status.toLowerCase().includes('evaluation');
              const isActionTaken = inc.status.toLowerCase().includes('action');

              return (
                <div key={inc.id} className={`incident-card ${isEmergency ? 'emergency' : isUrgent ? 'urgent' : ''}`}>
                  <div className="incident-header">
                    <div className="incident-title">
                      <span>{isEmergency ? '🚨' : isUrgent ? '⚠️' : 'ℹ️'}</span> {inc.originalDescription}
                    </div>
                    <span className={`risk-badge ${inc.riskLevel.toLowerCase()}`}>
                      {getRiskLabel(inc.riskLevel)}
                    </span>
                  </div>

                  <div style={{ display: 'flex', gap: '2rem', fontSize: '0.95rem', color: '#cbd5e1', flexWrap: 'wrap' }}>
                    <div><strong>Origen:</strong> {inc.origin === 'Voice' ? 'Voz' : inc.origin === 'Text' ? 'Texto' : 'Sensor'}</div>
                    <div>
                      <strong>Estado:</strong>{' '}
                      <span className={`status-tag ${isActionTaken ? 'actiontaken' : isUnderEval ? 'underevaluation' : ''}`}>
                        {getStatusLabel(inc.status)}
                      </span>
                    </div>
                    <div><strong>Detectado:</strong> {new Date(inc.timestamp).toLocaleString()}</div>
                  </div>

                  {/* Executed Actions History */}
                  {inc.actionsExecuted && inc.actionsExecuted.length > 0 && (
                    <div className="actions-history-box">
                      <div className="actions-history-header">
                        ⚡ Acciones Ejecutadas ({inc.actionsExecuted.length})
                      </div>
                      {inc.actionsExecuted.map((act) => (
                        <div key={act.id} className="action-item">
                          <span className="action-type-pill">
                            {act.actionType === 'NotifyFamily' ? '📲 Notificar Familia' :
                             act.actionType === 'ContactEmergencyServices' ? '🚑 Despacho 911' :
                             act.actionType === 'RequestMedicalAppointment' ? '📅 Turno Médico' : '📋 Guía de Cuidado'}
                          </span>
                          <span>{act.result}</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Actions Bar */}
                  <div style={{ display: 'flex', gap: '0.75rem', marginTop: '0.25rem', flexWrap: 'wrap', alignItems: 'center' }}>
                    {/* Preliminary Medical Evaluation Trigger */}
                    <button
                      className="btn-medical"
                      onClick={() => handleMedicalEvaluation(inc.id)}
                      disabled={evaluatingId === inc.id}
                      title="Ejecutar Evaluación Médica Preliminar (Reglas Duras + LLM + Criterios Sección 7)"
                    >
                      {evaluatingId === inc.id ? '🩺 Evaluando...' : '🩺 Evaluación Médica Preliminar'}
                    </button>

                    {/* Action Dispatch Button */}
                    <button
                      className="btn-dispatch"
                      onClick={() => handleDispatchActions(inc.id)}
                      disabled={dispatchingId === inc.id}
                      title="Ejecutar Protocolo de Gestión de Emergencias según Nivel de Riesgo"
                    >
                      {dispatchingId === inc.id ? '⚡ Despachando...' : isEmergency ? '🚨 Activar Protocolo 911' : '⚡ Despachar Acciones'}
                    </button>

                    {/* Rescue Sheet for First Responders */}
                    <button
                      className="btn-rescue-sheet"
                      onClick={() => handleViewRescueSheet(inc.id)}
                      disabled={loadingSheet}
                      title="Abrir Ficha Médica de Rescate de Acceso Rápido"
                    >
                      🚑 Ver Ficha de Rescate
                    </button>

                    {/* Medical Appointment (for Mild) */}
                    {inc.riskLevel.toLowerCase() === 'mild' && (
                      <button
                        className="btn-appointment"
                        onClick={() => handleRequestAppointment(inc.id)}
                        title="Programar consulta médica preventiva no urgente"
                      >
                        📅 Solicitar Turno Médico
                      </button>
                    )}

                    {/* Mark as False Alarm */}
                    {inc.status !== 'FalseAlarm' && (
                      <button
                        className="btn-false-alarm"
                        onClick={() => handleMarkFalseAlarm(inc.id)}
                        title="Marcar incidente como Falsa Alarma y retroalimentar el aprendizaje"
                      >
                        ⚠️ Falsa Alarma
                      </button>
                    )}

                    {inc.status !== 'Closed' && inc.status !== 'FalseAlarm' && (
                      <button
                        className="btn-large btn-secondary"
                        style={{ fontSize: '0.9rem', padding: '0.45rem 1rem' }}
                        onClick={() => handleEvaluateStatus(inc.id, 'Closed')}
                      >
                        Cerrar Incidente
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Rescue Medical Sheet Modal (First Responder Read-Only View) */}
      {activeRescueSheet && (
        <div className="modal-overlay" onClick={() => setActiveRescueSheet(null)}>
          <div className="rescue-modal" onClick={(e) => e.stopPropagation()}>
            <div className="rescue-header">
              <div>
                <h3>🚑 FICHA MÉDICA DE RESCATE (PRIMEROS RESPONDIENTES)</h3>
                <small style={{ color: '#94a3b8' }}>
                  Punto de Acceso Público de Emergencia: <code>GET /api/incidents/{activeRescueSheet.incidentId}/rescue-sheet</code>
                </small>
              </div>
              <button
                style={{ background: 'transparent', border: 'none', color: '#cbd5e1', fontSize: '1.5rem', cursor: 'pointer' }}
                onClick={() => setActiveRescueSheet(null)}
              >
                ✕
              </button>
            </div>

            <div className="rescue-grid">
              <div className="rescue-card">
                <span className="rescue-label">Nombre Completo del Paciente</span>
                <span className="rescue-value">{activeRescueSheet.patientFullName}</span>
                <small style={{ color: '#94a3b8' }}>DNI: {activeRescueSheet.nationalId} | {activeRescueSheet.age} años ({activeRescueSheet.gender})</small>
              </div>

              <div className="rescue-card">
                <span className="rescue-label">Grupo Sanguíneo</span>
                <span className="blood-type-badge">{activeRescueSheet.bloodType}</span>
              </div>

              <div className="rescue-card">
                <span className="rescue-label">Teléfono Principal y Domicilio</span>
                <span className="rescue-value">{activeRescueSheet.primaryPhone}</span>
                <small style={{ color: '#cbd5e1' }}>{activeRescueSheet.address}</small>
              </div>

              <div className="rescue-card">
                <span className="rescue-label">Obra Social / Cobertura Médica</span>
                <span className="rescue-value">{activeRescueSheet.healthInsurance}</span>
              </div>
            </div>

            <div className="rescue-card" style={{ borderColor: 'rgba(239, 68, 68, 0.4)' }}>
              <span className="rescue-label" style={{ color: '#ef4444' }}>⚠️ Alergias Conocidas</span>
              <div style={{ marginTop: '0.25rem' }}>
                {activeRescueSheet.knownAllergies.length === 0 ? (
                  <span style={{ color: '#94a3b8' }}>Sin alergias documentadas</span>
                ) : (
                  activeRescueSheet.knownAllergies.map((allg, idx) => (
                    <span key={idx} className="allergy-tag">⛔ {allg}</span>
                  ))
                )}
              </div>
            </div>

            <div className="rescue-grid">
              <div className="rescue-card">
                <span className="rescue-label">Enfermedades Preexistentes</span>
                <ul style={{ margin: '0.25rem 0 0 0', paddingLeft: '1.2rem', color: '#cbd5e1', fontSize: '0.95rem' }}>
                  {activeRescueSheet.preexistingConditions.map((c, idx) => (
                    <li key={idx}>{c}</li>
                  ))}
                </ul>
              </div>

              <div className="rescue-card">
                <span className="rescue-label">Medicación Habitual</span>
                <ul style={{ margin: '0.25rem 0 0 0', paddingLeft: '1.2rem', color: '#cbd5e1', fontSize: '0.95rem' }}>
                  {activeRescueSheet.currentMedication.map((m, idx) => (
                    <li key={idx}>{m}</li>
                  ))}
                </ul>
              </div>
            </div>

            <div className="rescue-card">
              <span className="rescue-label">Contactos de Emergencia ({activeRescueSheet.emergencyContacts.length})</span>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginTop: '0.35rem' }}>
                {activeRescueSheet.emergencyContacts.map((c, idx) => (
                  <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.95rem', color: '#cbd5e1', borderBottom: '1px solid rgba(255,255,255,0.05)', paddingBottom: '0.25rem' }}>
                    <span><strong>{c.contactName}</strong> ({c.relationship})</span>
                    <span>📞 {c.phone} <small style={{ color: '#38bdf8' }}>[{c.preferredMethod === 'Call' ? 'Llamada' : c.preferredMethod === 'SMS' ? 'SMS' : 'Push'}]</small></span>
                  </div>
                ))}
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', borderTop: '1px solid rgba(255,255,255,0.1)', paddingTop: '1rem' }}>
              <button
                className="btn-large btn-primary"
                style={{ padding: '0.5rem 1.5rem' }}
                onClick={() => window.print()}
              >
                🖨️ Imprimir Ficha de Rescate
              </button>
              <button
                className="btn-large btn-secondary"
                style={{ padding: '0.5rem 1.5rem' }}
                onClick={() => setActiveRescueSheet(null)}
              >
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
