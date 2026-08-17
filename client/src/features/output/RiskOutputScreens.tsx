import React, { useEffect, useState } from 'react';
import './RiskOutputScreens.css';

interface RescueSheetData {
  incidentId?: string;
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
  emergencyContacts: {
    contactName: string;
    relationship: string;
    phone: string;
    preferredMethod: string;
  }[];
}

interface IncidentDto {
  id: string;
  origin: string;
  originalDescription: string;
  riskLevel: string;
  status: string;
  timestamp: string;
}

interface RiskOutputScreensProps {
  activeUserId?: string;
}

export const RiskOutputScreens: React.FC<RiskOutputScreensProps> = ({ activeUserId }) => {
  const [selectedRisk, setSelectedRisk] = useState<'Mild' | 'Urgent' | 'PossibleEmergency'>('Mild');
  const [fullscreenEmergency, setFullscreenEmergency] = useState<boolean>(false);
  const [countdown, setCountdown] = useState<number>(30);
  const [countdownActive, setCountdownActive] = useState<boolean>(true);
  const [appointmentRequested, setAppointmentRequested] = useState<boolean>(false);
  const [dispatchCancelled, setDispatchCancelled] = useState<boolean>(false);
  const [dispatchedNow, setDispatchedNow] = useState<boolean>(false);

  // Real rescue sheet data from backend or fallback to seeded profile
  const [rescueSheet, setRescueSheet] = useState<RescueSheetData>({
    patientFullName: 'Elena Vasquez',
    nationalId: '30998877A',
    age: 80,
    gender: 'Femenino',
    primaryPhone: '+54 9 11 4433-2211',
    address: 'Calle Las Heras 1200, CABA',
    healthInsurance: 'OSDE 410 (Plan Platino)',
    bloodType: 'AB+',
    knownAllergies: ['Penicilina', 'Sulfamidas', 'Ibuprofeno'],
    preexistingConditions: ['Hipertensión Arterial Severa', 'Osteoporosis Avanzada', 'Arritmia Leve'],
    currentMedication: ['Amlodipina 10mg/día', 'Losartán 50mg', 'Calcio + Vitamina D3'],
    emergencyContacts: [
      { contactName: 'Sofia Vasquez', relationship: 'Hija', phone: '+54 9 11 9988-7766', preferredMethod: 'Llamada' },
      { contactName: 'Pablo Vasquez', relationship: 'Hijo', phone: '+54 9 11 8877-6655', preferredMethod: 'SMS' },
      { contactName: 'Dr. Alvarez', relationship: 'Médico de Cabecera', phone: '+54 9 11 7766-5544', preferredMethod: 'Push' }
    ]
  });

  // Fetch active user profile if activeUserId provided
  useEffect(() => {
    if (activeUserId) {
      fetch(`http://localhost:5000/api/users/${activeUserId}`)
        .then((r) => (r.ok ? r.json() : null))
        .then((user) => {
          if (user) {
            let age = 75;
            if (user.dateOfBirth) {
              const birthYear = new Date(user.dateOfBirth).getFullYear();
              age = new Date().getFullYear() - birthYear;
            }
            setRescueSheet({
              patientFullName: user.fullName,
              nationalId: user.nationalId,
              age,
              gender: user.gender,
              primaryPhone: user.primaryPhone,
              address: user.address,
              healthInsurance: user.healthInsurance || 'Particular',
              bloodType: user.bloodType || 'O+',
              knownAllergies: user.medicalProfile?.knownAllergies || [],
              preexistingConditions: user.medicalProfile?.preexistingConditions || [],
              currentMedication: user.medicalProfile?.currentMedication || [],
              emergencyContacts: user.emergencyContacts || []
            });
          }
        })
        .catch(() => {});
    }
  }, [activeUserId]);

  // Fetch real incidents from backend
  useEffect(() => {
    const url = activeUserId
      ? `http://localhost:5000/api/incidents?userId=${activeUserId}`
      : 'http://localhost:5000/api/incidents';
    fetch(url)
      .then((res) => res.json())
      .then((incidents: IncidentDto[]) => {
        if (incidents && incidents.length > 0) {
          const latest = incidents[0];
          fetch(`http://localhost:5000/api/incidents/${latest.id}/rescue-sheet`)
            .then((r) => (r.ok ? r.json() : null))
            .then((data) => {
              if (data) {
                setRescueSheet(data);
              }
            })
            .catch(() => {});
        }
      })
      .catch(() => {});
  }, [activeUserId]);

  // Countdown timer effect for Emergency Mode
  useEffect(() => {
    let timer: any;
    if (fullscreenEmergency && countdownActive && countdown > 0) {
      timer = setInterval(() => {
        setCountdown((prev) => prev - 1);
      }, 1000);
    } else if (countdown === 0 && countdownActive) {
      setDispatchedNow(true);
      setCountdownActive(false);
    }
    return () => clearInterval(timer);
  }, [fullscreenEmergency, countdownActive, countdown]);

  const handleRequestAppointment = async () => {
    setAppointmentRequested(true);
    try {
      await fetch('http://localhost:5000/api/incidents', { method: 'GET' })
        .then((res) => res.json())
        .then(async (incidents) => {
          if (incidents && incidents.length > 0) {
            await fetch(`http://localhost:5000/api/incidents/${incidents[0].id}/request-appointment`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ notes: 'Solicitud desde pantalla de salida (Situación Leve)' })
            });
          }
        });
    } catch (e) {
      console.log('Appointment request logged:', e);
    }
  };

  const handleCancelDispatch = () => {
    setCountdownActive(false);
    setDispatchCancelled(true);
  };

  const handleForceDispatch = () => {
    setCountdown(0);
    setCountdownActive(false);
    setDispatchedNow(true);
  };

  return (
    <div className="output-screens-container">
      {/* Control Header & Scenario Switcher */}
      <div className="output-control-header">
        <div>
          <h2>📱 Alerta y Recomendación</h2>
          <p style={{ color: '#94a3b8', margin: '0.25rem 0 0 0' }}>
            Comportamiento visual reactivo según el nivel de riesgo clasificado por el sistema.
          </p>
        </div>

        <div className="risk-screen-selector">
          <button
            className={`risk-tab-btn mild ${selectedRisk === 'Mild' ? 'active' : ''}`}
            onClick={() => setSelectedRisk('Mild')}
          >
            🟢 Situación Leve (Informativa)
          </button>

          <button
            className={`risk-tab-btn urgent ${selectedRisk === 'Urgent' ? 'active' : ''}`}
            onClick={() => setSelectedRisk('Urgent')}
          >
            🟡 Situación Urgente (Moderada)
          </button>

          <button
            className={`risk-tab-btn emergency ${selectedRisk === 'PossibleEmergency' ? 'active' : ''}`}
            onClick={() => setSelectedRisk('PossibleEmergency')}
          >
            🔴 Posible Emergencia (Crítica)
          </button>
        </div>
      </div>

      {/* ========================================================
          1. SITUACIÓN LEVE (AZUL / VERDE - INFORMATIVA)
          ======================================================== */}
      {selectedRisk === 'Mild' && (
        <div className="screen-mild-container">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
            <div className="mild-header-badge">
              <span>🟢</span> CLASIFICACIÓN: SITUACIÓN LEVE (INFORMATIVA)
            </div>
            <span style={{ color: '#34d399', fontWeight: 700, fontSize: '0.9rem' }}>
              ✓ Sin riesgo vital detectado | Monitoreo preventivo activo
            </span>
          </div>

          {/* Pantalla de Asistencia Interactiva */}
          <div className="transcript-chat-card">
            <div style={{ fontSize: '0.85rem', fontWeight: 800, color: '#38bdf8', textTransform: 'uppercase' }}>
              🎙️ Transcripción de la Conversación por Voz (Copiloto IA)
            </div>

            <div className="chat-bubble-ai">
              <strong>🤖 Asistente Guardián Digital:</strong>
              <div style={{ marginTop: '0.35rem' }}>
                "Detecté un mareo pasajero y leve molestia reportada. ¿Cómo te sientes en este momento? ¿El mareo se acompaña de palpitaciones o dolor en el pecho?"
              </div>
            </div>

            <div className="chat-bubble-patient">
              <strong>👤 Paciente:</strong>
              <div style={{ marginTop: '0.25rem' }}>
                "Me sentí un poco débil al levantarme de la cama, pero ya me senté y se me está pasando. No me duele el pecho."
              </div>
            </div>

            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: '0.25rem' }}>
              <span style={{ background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8', padding: '0.2rem 0.6rem', borderRadius: '6px', fontSize: '0.8rem', fontWeight: 700 }}>
                Síntoma: Mareo Ortostático Leve
              </span>
              <span style={{ background: 'rgba(52, 211, 153, 0.15)', color: '#34d399', padding: '0.2rem 0.6rem', borderRadius: '6px', fontSize: '0.8rem', fontWeight: 700 }}>
                SpO2: 98% Normal
              </span>
              <span style={{ background: 'rgba(52, 211, 153, 0.15)', color: '#34d399', padding: '0.2rem 0.6rem', borderRadius: '6px', fontSize: '0.8rem', fontWeight: 700 }}>
                Pulso: 74 BPM Regular
              </span>
            </div>
          </div>

          {/* Bloque de Recomendaciones */}
          <div className="recommendations-box">
            <h3>📋 Recomendaciones de Cuidado Clínico</h3>
            <ul className="recommendation-list">
              <li className="recommendation-item">
                <span className="rec-icon">🪑</span>
                <span><strong>Reposo preventivo:</strong> Favor de reposar sentado o recostado durante 15 minutos en un ambiente ventilado.</span>
              </li>
              <li className="recommendation-item">
                <span className="rec-icon">💧</span>
                <span><strong>Hidratación:</strong> Beber un vaso de agua fresca a pequeños sorbos para restablecer la volemia.</span>
              </li>
              <li className="recommendation-item">
                <span className="rec-icon">👁️</span>
                <span><strong>Observación continua:</strong> Observar si el síntoma persiste o si surgen mareos adicionales al incorporarse.</span>
              </li>
            </ul>
          </div>

          {/* Acción de Salida: Solicitar Turno Médico */}
          <div className="mild-actions-bar">
            {appointmentRequested ? (
              <div className="appointment-success-toast">
                <span>✓</span> Turno médico no urgente agendado con la red de atención primaria para seguimiento.
              </div>
            ) : (
              <button className="btn-appointment-mild" onClick={handleRequestAppointment}>
                📅 Solicitar Turno Médico (No Urgente)
              </button>
            )}
          </div>
        </div>
      )}

      {/* ========================================================
          2. SITUACIÓN URGENTE (AMARILLO / NARANJA - ALERTA MODERADA)
          ======================================================== */}
      {selectedRisk === 'Urgent' && (
        <div className="screen-urgent-container">
          {/* Notificación Destacada Parpadeante */}
          <div className="urgent-flashing-banner">
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <span style={{ fontSize: '1.8rem' }}>⚠️</span>
              <div>
                <div style={{ fontSize: '1.25rem' }}>ALERTA MÉDICA MODERADA — ATENCIÓN REQUERIDA</div>
                <small style={{ fontWeight: 'normal', opacity: 0.95 }}>
                  Síntomas de evolución clínica detectados. Se requiere consulta médica presencial en las próximas horas.
                </small>
              </div>
            </div>
            <span style={{ background: '#0f172a', color: '#fbbf24', padding: '0.4rem 0.8rem', borderRadius: '10px', fontSize: '0.85rem' }}>
              NIVEL: URGENTE
            </span>
          </div>

          {/* Estado de Enlaces (Notification Status Cards) */}
          <div>
            <h3 style={{ color: '#fbbf24', fontSize: '1.2rem', marginBottom: '0.75rem' }}>
              📲 Estado de Notificaciones a Contactos de Emergencia
            </h3>
            <div className="contact-links-grid">
              <div className="contact-link-card">
                <div className="contact-link-status">
                  <span style={{ fontWeight: 700, color: '#f8fafc' }}>📞 Sofia Vasquez (Hija)</span>
                  <span className="badge-delivered">✓ Llamada Enviada</span>
                </div>
                <small style={{ color: '#94a3b8' }}>Tel: +54 9 11 9988-7766 • Canal Preferido: Voz</small>
                <div style={{ fontSize: '0.85rem', color: '#cbd5e1', marginTop: '0.25rem' }}>
                  "Mensaje: Alerta de salud para Elena. Se requiere acompañamiento para control médico."
                </div>
              </div>

              <div className="contact-link-card">
                <div className="contact-link-status">
                  <span style={{ fontWeight: 700, color: '#f8fafc' }}>✉️ Pablo Vasquez (Hijo)</span>
                  <span className="badge-delivered">✓ SMS Entregado</span>
                </div>
                <small style={{ color: '#94a3b8' }}>Tel: +54 9 11 8877-6655 • Canal Preferido: SMS</small>
                <div style={{ fontSize: '0.85rem', color: '#cbd5e1', marginTop: '0.25rem' }}>
                  "Mensaje: Notificación preventiva enviada. Contacto de guardia notificado."
                </div>
              </div>

              <div className="contact-link-card">
                <div className="contact-link-status">
                  <span style={{ fontWeight: 700, color: '#f8fafc' }}>🔔 Dr. Alvarez (Médico)</span>
                  <span className="badge-delivered">✓ Push Entregado</span>
                </div>
                <small style={{ color: '#94a3b8' }}>Tel: +54 9 11 7766-5544 • Canal: App Médica</small>
                <div style={{ fontSize: '0.85rem', color: '#cbd5e1', marginTop: '0.25rem' }}>
                  "Alerta clínica remitida al panel del profesional de cabecera."
                </div>
              </div>
            </div>
          </div>

          {/* Recomendación Activa de Traslado */}
          <div className="travel-instruction-card">
            <h3>🏥 Recomendación Activa de Traslado Clínico</h3>
            <p style={{ margin: 0, color: '#f8fafc', fontSize: '1.05rem', lineHeight: 1.5 }}>
              Se sugiere trasladarse al centro médico o guardia más cercana de su cobertura médica:{' '}
              <strong style={{ color: '#fbbf24' }}>{rescueSheet.healthInsurance}</strong>.
            </p>
            <div style={{ background: 'rgba(234, 88, 12, 0.15)', borderLeft: '4px solid #ea580c', padding: '0.75rem 1rem', borderRadius: '8px', color: '#fed7aa', fontSize: '0.95rem' }}>
              ⚠️ <strong>Instrucción importante:</strong> No conducir ni desplazarse a pie en solitario si experimenta debilidad o mareos. Aguarde la llegada de un acompañante o solicite transporte asistido.
            </div>
          </div>
        </div>
      )}

      {/* ========================================================
          3. POSIBLE EMERGENCIA (ROJO - CRÍTICA & PANTALLA COMPLETA)
          ======================================================== */}
      {selectedRisk === 'PossibleEmergency' && (
        <div className="screen-emergency-preview">
          <div style={{ fontSize: '3rem' }}>🚨</div>
          <h2 style={{ color: '#ef4444', margin: 0, fontSize: '1.8rem', fontWeight: 900 }}>
            ALERTA CRÍTICA: PROTOCOLO DE EMERGENCIA AUTOMATIZADO
          </h2>
          <p style={{ color: '#fecaca', maxWidth: '650px', fontSize: '1.1rem', margin: 0 }}>
            Se ha detectado una situación de riesgo vital inminente. El sistema activa la superposición total sobre la pantalla, la cuenta regresiva de despacho 911 y la Ficha Médica de Rescate.
          </p>

          <button
            className="btn-open-fullscreen-emergency"
            onClick={() => {
              setFullscreenEmergency(true);
              setCountdown(30);
              setCountdownActive(true);
              setDispatchCancelled(false);
              setDispatchedNow(false);
            }}
          >
            🚨 ACTIVAR PANTALLA COMPLETA DE EMERGENCIA (OVERLAY TOTAL)
          </button>
        </div>
      )}

      {/* ========================================================
          TOTAL VIEWPORT OVERLAY (PANTALLA COMPLETA ROJA)
          ======================================================== */}
      {fullscreenEmergency && (
        <div className="emergency-fullscreen-overlay">
          {/* Barra Superior con Sirena */}
          <div className="emergency-top-bar">
            <div className="siren-title-group">
              <span className="siren-icon">🚨</span>
              <div>
                <h1>PROTOCOLO DE EMERGENCIA ACTIVADO</h1>
                <small style={{ color: '#fca5a5', fontSize: '1rem', fontWeight: 700 }}>
                  ALERTA MÁXIMA PRIORIDAD • RIESGO VITAL DETECTADO
                </small>
              </div>
            </div>

            <button
              onClick={() => setFullscreenEmergency(false)}
              style={{
                background: 'rgba(255, 255, 255, 0.15)',
                border: '1px solid rgba(255, 255, 255, 0.3)',
                color: '#ffffff',
                padding: '0.6rem 1.2rem',
                borderRadius: '10px',
                fontWeight: 800,
                cursor: 'pointer',
                fontSize: '1rem'
              }}
            >
              ✕ Cerrar Overlay
            </button>
          </div>

          {/* Cronómetro de Despacho Automático */}
          <div className="countdown-section">
            <div className="countdown-desc">
              {dispatchCancelled ? (
                <span style={{ color: '#fbbf24', fontSize: '1.3rem' }}>🛑 DESPACHO AUTOMÁTICO CANCELADO POR EL USUARIO</span>
              ) : dispatchedNow || countdown === 0 ? (
                <span style={{ color: '#4ade80', fontSize: '1.4rem' }}>🚑 ¡UNIDADES DE EMERGENCIA 911 / EMS DESPACHADAS!</span>
              ) : (
                <span>DESPACHO AUTOMÁTICO HACIA SERVICIOS DE EMERGENCIA (911 / EMS) EN:</span>
              )}
            </div>

            {!dispatchCancelled && !dispatchedNow && (
              <div className="countdown-timer-circle">
                00:{countdown.toString().padStart(2, '0')}s
              </div>
            )}

            <div className="countdown-buttons-bar">
              {!dispatchCancelled && !dispatchedNow && (
                <>
                  <button className="btn-cancel-dispatch" onClick={handleCancelDispatch}>
                    🛑 Cancelar Despacho (Falsa Alarma)
                  </button>
                  <button className="btn-immediate-dispatch" onClick={handleForceDispatch}>
                    📞 Despachar Inmediatamente (911)
                  </button>
                </>
              )}

              {(dispatchCancelled || dispatchedNow) && (
                <button
                  className="btn-cancel-dispatch"
                  style={{ background: '#22c55e', color: '#0f172a', borderColor: '#4ade80' }}
                  onClick={() => {
                    setCountdown(30);
                    setCountdownActive(true);
                    setDispatchCancelled(false);
                    setDispatchedNow(false);
                  }}
                >
                  🔄 Reiniciar Cuenta Regresiva (30s)
                </button>
              )}
            </div>
          </div>

          {/* Ficha Médica de Rescate (Zero Friction para Rescatistas) */}
          <div className="emergency-rescue-sheet">
            <h2>
              <span>🚑</span> FICHA MÉDICA DE RESCATE (LECTURA INMEDIATA PARA PARAMÉDICOS / PRIMER RESPONDIENTE)
            </h2>

            <div className="emergency-vitals-banner">
              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Paciente</span>
                <span style={{ fontSize: '1.4rem', fontWeight: 800, color: '#f8fafc' }}>
                  {rescueSheet.patientFullName}
                </span>
                <small style={{ color: '#cbd5e1' }}>
                  DNI: {rescueSheet.nationalId} • {rescueSheet.age} años ({rescueSheet.gender})
                </small>
              </div>

              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Grupo Sanguíneo</span>
                <span className="emergency-blood-badge">{rescueSheet.bloodType}</span>
              </div>

              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Cobertura Médica</span>
                <span style={{ fontSize: '1.2rem', fontWeight: 700, color: '#f8fafc' }}>
                  {rescueSheet.healthInsurance}
                </span>
              </div>

              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Dirección & Teléfono</span>
                <span style={{ fontSize: '1.05rem', fontWeight: 600, color: '#f8fafc' }}>
                  {rescueSheet.address}
                </span>
                <small style={{ color: '#38bdf8' }}>Tel: {rescueSheet.primaryPhone}</small>
              </div>
            </div>

            {/* Alergias Conocidas */}
            <div className="emergency-vital-box" style={{ borderColor: '#ef4444' }}>
              <span className="emergency-vital-label" style={{ color: '#f87171' }}>⚠️ Alergias Medicamentosas Conocidas</span>
              <div className="emergency-allergy-list">
                {rescueSheet.knownAllergies.map((allg, idx) => (
                  <span key={idx} className="emergency-allergy-tag">⛔ {allg}</span>
                ))}
              </div>
            </div>

            {/* Condiciones Preexistentes y Medicación */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1rem' }}>
              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Enfermedades Preexistentes</span>
                <ul style={{ margin: '0.35rem 0 0 0', paddingLeft: '1.2rem', color: '#cbd5e1', fontSize: '0.95rem' }}>
                  {rescueSheet.preexistingConditions.map((c, idx) => (
                    <li key={idx}><strong>{c}</strong></li>
                  ))}
                </ul>
              </div>

              <div className="emergency-vital-box">
                <span className="emergency-vital-label">Medicación Actual</span>
                <ul style={{ margin: '0.35rem 0 0 0', paddingLeft: '1.2rem', color: '#cbd5e1', fontSize: '0.95rem' }}>
                  {rescueSheet.currentMedication.map((m, idx) => (
                    <li key={idx}>{m}</li>
                  ))}
                </ul>
              </div>
            </div>

            {/* Contactos de Emergencia Directos */}
            <div className="emergency-vital-box">
              <span className="emergency-vital-label">Contactos Notificados de Inmediato</span>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '0.75rem', marginTop: '0.5rem' }}>
                {rescueSheet.emergencyContacts.map((c, idx) => (
                  <div key={idx} style={{ background: '#0f172a', padding: '0.6rem 0.8rem', borderRadius: '8px', border: '1px solid #334155' }}>
                    <div style={{ fontWeight: 700, color: '#f8fafc' }}>{c.contactName} ({c.relationship})</div>
                    <div style={{ color: '#38bdf8', fontSize: '0.9rem', marginTop: '0.2rem' }}>📞 {c.phone}</div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
