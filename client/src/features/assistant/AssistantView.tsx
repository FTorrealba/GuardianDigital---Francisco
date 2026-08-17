import React, { useState } from 'react';
import './Assistant.css';

interface SymptomResponse {
  incidentId: string;
  origin: string;
  detectedSymptoms: string[];
  suggestedQuestions: string[];
  suggestedUrgencyLevel: 'mild' | 'urgent' | 'possible_emergency';
  conversationalResponse: string;
  timestamp: string;
}

interface ChatItem {
  id: string;
  sender: 'user' | 'agent';
  origin?: string;
  text: string;
  timestamp: string;
  data?: SymptomResponse;
}

interface AssistantViewProps {
  activeUserId?: string;
  activeUserName?: string;
}

export const AssistantView: React.FC<AssistantViewProps> = ({ activeUserId, activeUserName }) => {
  const [activeTab, setActiveTab] = useState<'voice' | 'text'>('voice');
  const [inputText, setInputText] = useState<string>('');
  const [isRecording, setIsRecording] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(false);
  const [chatHistory, setChatHistory] = useState<ChatItem[]>([
    {
      id: 'welcome-1',
      sender: 'agent',
      text: `¡Hola${activeUserName ? ` ${activeUserName}` : ''}! Soy tu asistente de seguridad Guardián Digital. Puedes hablar o escribir cualquier síntoma o molestia que sientas, y evaluaré tu situación de inmediato.`,
      timestamp: new Date().toLocaleTimeString(),
    },
  ]);

  const reportSymptom = async (message: string, origin: 'Voice' | 'Text') => {
    if (!message.trim() || loading) return;

    const userItemId = `user-${Date.now()}`;
    const newChat: ChatItem[] = [
      ...chatHistory,
      {
        id: userItemId,
        sender: 'user',
        origin,
        text: message,
        timestamp: new Date().toLocaleTimeString(),
      },
    ];

    setChatHistory(newChat);
    setInputText('');
    setLoading(true);

    try {
      const res = await fetch('http://localhost:5000/api/incidents/report-symptom', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message, origin, userId: activeUserId || undefined }),
      });

      if (res.ok) {
        const data: SymptomResponse = await res.json();
        setChatHistory((prev) => [
          ...prev,
          {
            id: `agent-${Date.now()}`,
            sender: 'agent',
            text: data.conversationalResponse,
            timestamp: new Date(data.timestamp).toLocaleTimeString(),
            data,
          },
        ]);
      } else {
        const errorData = await res.json().catch(() => null);
        setChatHistory((prev) => [
          ...prev,
          {
            id: `err-${Date.now()}`,
            sender: 'agent',
            text: `⚠️ Error del Sistema: ${errorData?.error || 'No se pudo interpretar el reporte de síntomas.'}`,
            timestamp: new Date().toLocaleTimeString(),
          },
        ]);
      }
    } catch (err: any) {
      console.error('Error al reportar síntoma:', err);
      setChatHistory((prev) => [
        ...prev,
        {
          id: `err-${Date.now()}`,
          sender: 'agent',
          text: `⚠️ Error de comunicación con el backend (${err.message || 'Verifique la conexión'}).`,
          timestamp: new Date().toLocaleTimeString(),
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleMicClick = () => {
    if (isRecording) {
      setIsRecording(false);
      return;
    }

    setIsRecording(true);
    // Simulate voice recording for 2.2 seconds then trigger a sample voice report in Spanish
    setTimeout(() => {
      setIsRecording(false);
      const voiceSamples = [
        "Me cuesta respirar y siento una fuerte opresión en el pecho que se me va al brazo",
        "Siento un adormecimiento súbito en el lado izquierdo de la cara y se me cae el brazo",
        "Tengo fiebre muy alta de 39.5 grados y escalofríos intensos",
        "Me resbalé en el baño y me duele mucho la cadera, no me puedo levantar",
        "Tengo un dolor de cabeza leve y cansancio por estar mucho tiempo frente a la pantalla",
      ];
      const randomSample = voiceSamples[Math.floor(Math.random() * voiceSamples.length)];
      reportSymptom(randomSample, 'Voice');
    }, 2200);
  };

  const handleSendText = () => {
    if (!inputText.trim()) return;
    reportSymptom(inputText, 'Text');
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      handleSendText();
    }
  };

  return (
    <div className="assistant-container">
      {/* Header */}
      <div className="assistant-header">
        <div>
          <h2>🤖 Copiloto de Seguridad y Triaje con IA</h2>
          <p style={{ color: '#94a3b8', margin: '0.25rem 0 0 0', fontSize: '0.9rem' }}>
            Motor de Interpretación Clínica en Lenguaje Natural & Triaje de Emergencias
          </p>
        </div>
        <div className="mode-tabs">
          <button
            className={`mode-tab-btn ${activeTab === 'voice' ? 'active' : ''}`}
            onClick={() => setActiveTab('voice')}
          >
            🎙️ Entrada por Voz
          </button>
          <button
            className={`mode-tab-btn ${activeTab === 'text' ? 'active' : ''}`}
            onClick={() => setActiveTab('text')}
          >
            💬 Entrada por Texto
          </button>
        </div>
      </div>

      {/* Quick Preset Scenarios (20+ Samples Ready) */}
      <div className="presets-section">
        <span className="presets-title">⚡ Casos Clínicos de Prueba Rápida (Haga clic para evaluar con el LLM)</span>

        <div className="preset-chips-group">
          <button
            className="preset-chip emergency"
            onClick={() => reportSymptom("Me cuesta respirar y siento una fuerte opresión en el pecho que se me va al brazo", 'Voice')}
          >
            🚨 Dolor de Pecho con Irradiación
          </button>
          <button
            className="preset-chip emergency"
            onClick={() => reportSymptom("Siento la cara caída del lado izquierdo y el brazo sin fuerza", 'Voice')}
          >
            🚨 Parálisis Facial y Brazo Débil (FAST)
          </button>
          <button
            className="preset-chip emergency"
            onClick={() => reportSymptom("Tengo el peor dolor de cabeza de mi vida, fue como un trueno repentino", 'Voice')}
          >
            🚨 Cefalea en Trueno / Brusca
          </button>
          <button
            className="preset-chip emergency"
            onClick={() => reportSymptom("Se me está cerrando la garganta por una reacción alérgica severa", 'Voice')}
          >
            🚨 Asfixia / Edema de Glotis
          </button>
          <button
            className="preset-chip urgent"
            onClick={() => reportSymptom("Tengo fiebre alta de 39 grados y escalofríos intensos", 'Text')}
          >
            ⚠️ Fiebre Alta (39°C) y Escalofríos
          </button>
          <button
            className="preset-chip urgent"
            onClick={() => reportSymptom("Siento mucho mareo y la habitación me da vueltas", 'Voice')}
          >
            ⚠️ Vértigo Severo e Inestabilidad
          </button>
          <button
            className="preset-chip urgent"
            onClick={() => reportSymptom("El corazón me late a mil estando sentado, siento palpitaciones fuertes", 'Text')}
          >
            ⚠️ Taquicardia y Palpitaciones
          </button>
          <button
            className="preset-chip urgent"
            onClick={() => reportSymptom("Me caí al lado de la cama, me duele la cadera y no puedo pararme", 'Voice')}
          >
            ⚠️ Caída con Dificultad para Caminar
          </button>
          <button
            className="preset-chip"
            onClick={() => reportSymptom("Tengo un dolor de cabeza leve por estar frente a la pantalla", 'Text')}
          >
            ℹ️ Cefalea Tensional Leve
          </button>
          <button
            className="preset-chip"
            onClick={() => reportSymptom("Tengo dolor de garganta leve y un poco de congestión nasal", 'Text')}
          >
            ℹ️ Resfrío Leve y Congestión
          </button>
          <button
            className="preset-chip"
            onClick={() => reportSymptom("Tengo cansancio y dolor muscular leve después de caminar", 'Text')}
          >
            ℹ️ Fatiga y Dolor Muscular
          </button>
        </div>
      </div>

      {/* Input Box Area */}
      {activeTab === 'voice' ? (
        <div className="voice-box">
          <button
            className={`mic-button ${isRecording ? 'recording' : ''}`}
            onClick={handleMicClick}
            disabled={loading}
            title="Haga clic para simular captura de voz"
          >
            🎙️
          </button>
          <div style={{ color: isRecording ? '#fca5a5' : '#cbd5e1', fontWeight: 600 }}>
            {isRecording
              ? '🔴 Grabando telemetría de voz... (Hable ahora)'
              : 'Haga clic en el Micrófono para simular un reporte de síntomas por voz'}
          </div>
          <small style={{ color: '#94a3b8' }}>
            Simula el flujo de audio enviado al endpoint <code>POST /api/incidents/report-symptom</code>
          </small>
        </div>
      ) : (
        <div className="chat-input-bar">
          <input
            type="text"
            className="chat-input"
            placeholder="Escriba sus síntomas (ej. 'Siento un mareo fuerte y me duele el pecho')..."
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={loading}
          />
          <button
            className="send-btn"
            onClick={handleSendText}
            disabled={loading || !inputText.trim()}
          >
            {loading ? 'Interpretando...' : '➤ Enviar'}
          </button>
        </div>
      )}

      {/* Conversation & Triage Feed */}
      <div className="conversation-feed">
        {chatHistory.map((msg) => {
          if (msg.sender === 'user') {
            return (
              <div key={msg.id} className="chat-message">
                <div className="user-bubble">
                  <div style={{ fontSize: '0.8rem', opacity: 0.8, marginBottom: '0.2rem' }}>
                    {msg.origin === 'Voice' ? '🎙️ Telemetría de Voz' : '💬 Mensaje del Paciente'} • {msg.timestamp}
                  </div>
                  <div>"{msg.text}"</div>
                </div>
              </div>
            );
          }

          const data = msg.data;
          const urgency = data?.suggestedUrgencyLevel || 'mild';

          return (
            <div key={msg.id} className="chat-message">
              <div className={`agent-response-card ${urgency}`}>
                <div className="agent-header-row">
                  <div className="agent-title">
                    <span>🛡️</span> Agente de Interacción Guardián Digital
                  </div>
                  {data && (
                    <span className={`urgency-badge ${urgency}`}>
                      {urgency === 'possible_emergency' ? '🚨 Posible Emergencia' : urgency === 'urgent' ? '⚠️ Urgente' : 'ℹ️ Malestar Leve'}
                    </span>
                  )}
                </div>

                <div className="conversational-text">{msg.text}</div>

                {data && (
                  <>
                    {/* Detected Symptoms */}
                    <div className="symptoms-tags-row">
                      <span style={{ fontSize: '0.8rem', color: '#94a3b8', fontWeight: 600 }}>
                        Síntomas Detectados:
                      </span>
                      {data.detectedSymptoms.map((s, idx) => (
                        <span key={idx} className="symptom-tag">
                          ✓ {s}
                        </span>
                      ))}
                    </div>

                    {/* Clinically Relevant Follow-up Questions */}
                    {data.suggestedQuestions.length > 0 && (
                      <div className="questions-box">
                        <div className="questions-header">
                          🩺 Preguntas de Seguimiento Clínico (No Diagnósticas)
                        </div>
                        {data.suggestedQuestions.map((q, idx) => (
                          <div key={idx} className="question-item">
                            {idx + 1}. {q}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Incident Metadata */}
                    <div className="incident-badge-row">
                      <span>
                        Incidente Registrado: <code>#{data.incidentId.substring(0, 8)}...</code>
                      </span>
                      <span>Origen: <strong>{data.origin === 'Voice' ? 'Voz' : 'Texto'}</strong></span>
                      <span>Estado: <strong style={{ color: '#38bdf8' }}>Detectado</strong></span>
                    </div>
                  </>
                )}
              </div>
            </div>
          );
        })}

        {loading && (
          <div className="chat-message">
            <div className="agent-response-card" style={{ fontStyle: 'italic', color: '#94a3b8' }}>
              🤖 El LLM de Guardián Digital está interpretando los síntomas y evaluando la urgencia clínica...
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
