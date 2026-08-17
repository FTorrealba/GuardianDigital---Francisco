import React, { useEffect, useState } from 'react';
import './Dashboard.css';

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

interface DashboardViewProps {
  activeUserId?: string;
  activeUserName?: string;
}

export const DashboardView: React.FC<DashboardViewProps> = ({ activeUserId: _activeUserId, activeUserName }) => {
  const [devices, setDevices] = useState<DeviceDto[]>([]);
  const [learningStats, setLearningStats] = useState<LearningStatsDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [lastInjected, setLastInjected] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const fetchDevices = async () => {
    try {
      const res = await fetch('http://localhost:5000/api/devices');
      if (res.ok) {
        const data: DeviceDto[] = await res.json();
        setDevices(data);
        setErrorMsg(null);
      }
    } catch (err: any) {
      console.error('Error al obtener dispositivos:', err);
    } finally {
      setLoading(false);
    }
  };

  const fetchLearningStats = async () => {
    try {
      const res = await fetch('http://localhost:5000/api/learning/stats');
      if (res.ok) {
        const stats: LearningStatsDto = await res.json();
        setLearningStats(stats);
      }
    } catch (err) {
      console.error('Error al obtener estadísticas de aprendizaje:', err);
    }
  };

  const seedDevices = async () => {
    try {
      setLoading(true);
      const res = await fetch('http://localhost:5000/api/devices/seed', { method: 'POST' });
      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Error al inicializar dispositivos');
      }
      await fetchDevices();
      await fetchLearningStats();
    } catch (err: any) {
      setErrorMsg(err.message || 'Error al vincular dispositivos.');
    } finally {
      setLoading(false);
    }
  };

  const injectAnomaly = async (anomalyType: string, deviceId?: string) => {
    try {
      const res = await fetch('http://localhost:5000/api/devices/inject-anomaly', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ deviceId, anomalyType }),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error || 'Error al inyectar anomalía');
      }

      setLastInjected(`⚠️ ${data.message} (${data.value})`);
      await fetchDevices();
      await fetchLearningStats();
    } catch (err: any) {
      setErrorMsg(err.message || 'Error al inyectar anomalía.');
    }
  };

  useEffect(() => {
    fetchDevices();
    fetchLearningStats();
    // Poll telemetry and learning stats every 3 seconds
    const interval = setInterval(() => {
      fetchDevices();
      fetchLearningStats();
    }, 3000);
    return () => clearInterval(interval);
  }, []);

  const getDeviceIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'biometricband':
      case 'smartwatch':
        return '⌚';
      case 'pulseoximeter':
        return '🫁';
      case 'motionsensor':
        return '🏃';
      case 'doorsensor':
        return '🚪';
      case 'camera':
        return '📹';
      case 'microphone':
        return '🎙️';
      default:
        return '📡';
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
        return 'Micrófono de Detección Acústica';
      default:
        return type;
    }
  };

  const maxReadings = learningStats?.hourlyActivityDistribution
    ? Math.max(...learningStats.hourlyActivityDistribution.map((h) => h.readingCount), 1)
    : 1;

  return (
    <div className="simulator-container">
      <div className="simulator-header">
        <div>
          <h2>📡 Simulador de Sensores & Telemetría Sintética</h2>
          <p style={{ color: '#cbd5e1', margin: '0.25rem 0 0 0' }}>
            Generación continua de telemetría para <strong>{activeUserName || 'el paciente activo'}</strong> con depuración automática cada 24 horas.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
          <div className="telemetry-pulse">
            <span className="green-dot"></span>
            Generador Activo en Segundo Plano
          </div>
          {devices.length === 0 && (
            <button className="btn-large btn-primary" onClick={seedDevices}>
              🌱 Vincular Sensores Predeterminados
            </button>
          )}
        </div>
      </div>

      {lastInjected && (
        <div className="injection-banner">
          <span>{lastInjected}</span>
          <button
            onClick={() => setLastInjected(null)}
            style={{ background: 'none', border: 'none', color: '#4ade80', fontSize: '1.2rem', cursor: 'pointer' }}
          >
            ×
          </button>
        </div>
      )}

      {errorMsg && (
        <div className="error-banner" style={{ margin: 0 }}>
          ⚠️ {errorMsg}
        </div>
      )}

      {/* Manual Anomaly Injection Control Panel */}
      <div className="anomaly-panel">
        <h3>🚨 Inyector Manual de Anomalías y Eventos Críticos</h3>
        <p style={{ color: '#cbd5e1', margin: '0.25rem 0 1rem 0', fontSize: '0.95rem' }}>
          Dispara lecturas fuera de rango para validar el análisis y triaje autónomo de la IA.
        </p>

        <div className="anomaly-buttons-grid">
          <button className="btn-anomaly" onClick={() => injectAnomaly('Fall')}>
            <span className="icon">🚨</span>
            <span>Simular Impacto por Caída Brusca</span>
            <small style={{ fontWeight: 'normal', opacity: 0.8 }}>Aceleración: Vector 5.2G</small>
          </button>

          <button className="btn-anomaly" onClick={() => injectAnomaly('Tachycardia')}>
            <span className="icon">❤️</span>
            <span>Simular Taquicardia Severa</span>
            <small style={{ fontWeight: 'normal', opacity: 0.8 }}>Ritmo Cardíaco: 172 BPM</small>
          </button>

          <button className="btn-anomaly" onClick={() => injectAnomaly('Immobility')}>
            <span className="icon">🛑</span>
            <span>Simular Inmovilidad Prolongada</span>
            <small style={{ fontWeight: 'normal', opacity: 0.8 }}>Movimiento: Nulo durante 240m</small>
          </button>

          <button className="btn-anomaly" onClick={() => injectAnomaly('Hypoxia')}>
            <span className="icon">🫁</span>
            <span>Simular Hipoxia Crítica</span>
            <small style={{ fontWeight: 'normal', opacity: 0.8 }}>SpO2: 81% Saturación</small>
          </button>

          <button className="btn-anomaly" onClick={() => injectAnomaly('DoorForced')}>
            <span className="icon">🚪</span>
            <span>Simular Apertura Forzada</span>
            <small style={{ fontWeight: 'normal', opacity: 0.8 }}>Sensor Puerta: Intrusión</small>
          </button>
        </div>
      </div>

      {/* Point 8: Persistent Memory & Learning Agent Analytics */}
      {learningStats && (
        <div className="learning-panel">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
            <h3>🧠 Memoria Persistente & Analítica del Agente de Aprendizaje</h3>
            <small style={{ color: '#94a3b8' }}>
              Agregando {learningStats.totalTelemetryReadingsAnalyzed} lecturas de telemetría y {learningStats.totalIncidentsLast30Days} incidentes analizados (30 días)
            </small>
          </div>

          <div className="learning-metrics-grid">
            <div className="learning-card">
              <span className="learning-card-label">Tasa de Falsas Alarmas (30 Días)</span>
              <span className="learning-card-value" style={{ color: learningStats.falseAlarmPercentage > 25 ? '#ef4444' : '#22c55e' }}>
                {learningStats.falseAlarmPercentage}%
              </span>
              <span className="learning-card-sub">
                {learningStats.falseAlarmsLast30Days} de {learningStats.totalIncidentsLast30Days} incidentes marcados como falsa alarma
              </span>
            </div>

            <div className="learning-card">
              <span className="learning-card-label">Precisión del Modelo de Detección</span>
              <span className="learning-card-value" style={{ color: '#38bdf8' }}>
                {learningStats.precisionScore}%
              </span>
              <span className="learning-card-sub">
                Confianza del modelo y calibración adaptativa
              </span>
            </div>

            <div className="learning-card">
              <span className="learning-card-label">Ventana de Mayor Actividad Diaria</span>
              <span className="learning-card-value" style={{ color: '#facc15' }}>
                {learningStats.peakActivityWindow}
              </span>
              <span className="learning-card-sub">
                Ventana de Descanso / Noche: {learningStats.restActivityWindow}
              </span>
            </div>
          </div>

          {/* 24-Hour Typical Activity Distribution */}
          <div className="hourly-timeline-box">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 700, color: '#38bdf8', textTransform: 'uppercase' }}>
                📊 Densidad Típica de Telemetría en 24 Horas (00:00 - 23:00)
              </span>
              <div style={{ display: 'flex', gap: '1rem', fontSize: '0.75rem' }}>
                <span style={{ color: '#22c55e' }}>● Pico</span>
                <span style={{ color: '#38bdf8' }}>● Moderado</span>
                <span style={{ color: '#64748b' }}>● Bajo</span>
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

      {/* Live Devices & Telemetry Streams Grid */}
      <div>
        <h3 style={{ color: '#f8fafc', fontSize: '1.4rem', marginBottom: '1rem' }}>
          Sensores y Dispositivos Vinculados ({devices.length})
        </h3>

        {loading && devices.length === 0 ? (
          <p style={{ color: '#94a3b8' }}>Cargando dispositivos de telemetría...</p>
        ) : devices.length === 0 ? (
          <div style={{ background: '#1e293b', padding: '2rem', borderRadius: '12px', textAlign: 'center' }}>
            <p style={{ fontSize: '1.2rem', color: '#cbd5e1' }}>No hay dispositivos vinculados para el usuario activo.</p>
            <button className="btn-large btn-primary" onClick={seedDevices}>
              Vincular 6 Dispositivos de Telemetría
            </button>
          </div>
        ) : (
          <div className="devices-grid">
            {devices.map((dev) => (
              <div key={dev.id} className="device-card">
                <div className="device-header">
                  <div className="device-type">
                    <span>{getDeviceIcon(dev.type)}</span>
                    <span>{getDeviceDisplayName(dev.type)}</span>
                  </div>
                  <span className={`status-pill ${dev.isTransmitting ? 'transmitting' : 'inactive'}`}>
                    {dev.isTransmitting && <span className="green-dot"></span>}
                    {dev.isTransmitting ? 'Transmitiendo' : 'Inactivo'}
                  </span>
                </div>

                <div>
                  <div style={{ fontSize: '0.85rem', color: '#94a3b8', marginBottom: '0.5rem' }}>
                    Lecturas Recientes de Telemetría ({dev.recentReadings.length}):
                  </div>
                  <div className="readings-history">
                    {dev.recentReadings.length === 0 ? (
                      <div className="reading-item">
                        <span style={{ color: '#94a3b8' }}>Esperando el primer tick de telemetría...</span>
                      </div>
                    ) : (
                      dev.recentReadings.map((r) => {
                        const isAnomalous =
                          r.value.includes('CRITICAL') ||
                          r.value.includes('SEVERE') ||
                          r.value.includes('UNAUTHORIZED') ||
                          r.value.includes('IMMOBILITY');
                        return (
                          <div key={r.id} className={`reading-item ${isAnomalous ? 'anomalous' : ''}`}>
                            <span className="reading-val">{r.value}</span>
                            <span className="reading-time">
                              {new Date(r.timestamp).toLocaleTimeString()}
                            </span>
                          </div>
                        );
                      })
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
