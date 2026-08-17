import { useEffect, useState } from 'react';
import './App.css';
import { OnboardingView } from './features/onboarding/OnboardingView';
import { ProfileView } from './features/profile/ProfileView';
import { DashboardView } from './features/dashboard/DashboardView';
import { AlertsView } from './features/alerts/AlertsView';
import { AssistantView } from './features/assistant/AssistantView';
import { AnalyticsView } from './features/analytics/AnalyticsView';
import { RiskOutputScreens } from './features/output/RiskOutputScreens';

interface HealthData {
  status: string;
  message: string;
  timestamp: string;
  databaseRecordCount: number;
}

export interface UserSummaryDto {
  id: string;
  fullName: string;
  nationalId: string;
  dateOfBirth?: string;
  gender: string;
  primaryPhone: string;
  address: string;
  healthInsurance?: string;
  bloodType: string;
}

type TabType = 'output' | 'analytics' | 'alerts' | 'assistant' | 'simulator' | 'onboarding' | 'profile';

interface NavItem {
  id: TabType;
  icon: string;
  label: string;
}

const NAV_ITEMS: NavItem[] = [
  { id: 'output', icon: '📱', label: 'Pantallas de Alerta' },
  { id: 'analytics', icon: '🧠', label: 'Panel Analítico de IA' },
  { id: 'alerts', icon: '🚨', label: 'Monitor de Incidentes & Triaje' },
  { id: 'assistant', icon: '🎙️', label: 'Copiloto de Voz & Texto' },
  { id: 'simulator', icon: '📡', label: 'Simulador de Sensores' },
  { id: 'profile', icon: '👤', label: 'Perfil Clínico Activo' },
  { id: 'onboarding', icon: '📝', label: 'Registro de Nuevo Paciente' },
];

export function App() {
  const [health, setHealth] = useState<HealthData | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabType>('output');
  const [sidebarCollapsed, setSidebarCollapsed] = useState<boolean>(false);
  const [users, setUsers] = useState<UserSummaryDto[]>([]);
  const [activeUserId, setActiveUserId] = useState<string>('');

  const checkHealth = () => {
    setLoading(true);
    setError(null);
    fetch('http://localhost:5000/health')
      .then((res) => {
        if (!res.ok) {
          throw new Error(`El servidor devolvió HTTP ${res.status}`);
        }
        return res.json();
      })
      .then((data: HealthData) => {
        setHealth(data);
        setLoading(false);
      })
      .catch((err) => {
        console.error('La verificación de estado falló:', err);
        setError(err.message || 'Error de conexión');
        setLoading(false);
      });
  };

  const fetchUsers = (preferredUserId?: string) => {
    fetch('http://localhost:5000/api/users')
      .then((res) => (res.ok ? res.json() : []))
      .then((data: UserSummaryDto[]) => {
        setUsers(data);
        if (preferredUserId) {
          setActiveUserId(preferredUserId);
        } else if (data.length > 0 && (!activeUserId || !data.some((u) => u.id === activeUserId))) {
          setActiveUserId(data[0].id);
        }
      })
      .catch(() => {});
  };

  useEffect(() => {
    checkHealth();
    fetchUsers();
  }, []);

  const handleUserCreated = (newUserId: string) => {
    fetchUsers(newUserId);
    setActiveTab('profile');
  };

  const handleProfileDeleted = () => {
    fetch('http://localhost:5000/api/users')
      .then((res) => (res.ok ? res.json() : []))
      .then((data: UserSummaryDto[]) => {
        setUsers(data);
        if (data.length > 0) {
          setActiveUserId(data[0].id);
          setActiveTab('profile');
        } else {
          setActiveUserId('');
          setActiveTab('onboarding');
        }
      })
      .catch(() => {
        setActiveUserId('');
        setActiveTab('onboarding');
      });
  };

  const activeUser = users.find((u) => u.id === activeUserId);
  const activeUserName = activeUser ? activeUser.fullName : 'Paciente Principal';

  return (
    <div className="app-shell">
      {/* Top Header Bar */}
      <header className="app-header">
        <div className="logo-group">
          <button
            className="sidebar-toggle-btn"
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            title={sidebarCollapsed ? 'Expandir menú lateral' : 'Minimizar menú lateral'}
          >
            ☰
          </button>
          <span className="shield-icon">🛡️</span>
          <h1>Guardián Digital</h1>
          <span className="badge">Monitoreo & IA</span>
        </div>

        <div className="header-actions">
          {/* Active Patient Switcher Dropdown */}
          {users.length > 0 && (
            <div className="patient-selector-container">
              <span className="patient-selector-label">👤 Paciente Activo:</span>
              <select
                className="patient-selector-select"
                value={activeUserId}
                onChange={(e) => setActiveUserId(e.target.value)}
                title="Selecciona el paciente activo para ver su ficha, incidentes y telemetría"
              >
                {users.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.fullName} (DNI: {u.nationalId})
                  </option>
                ))}
              </select>
            </div>
          )}

          <div className="connection-status">
            {loading && <span className="status-indicator loading">Comprobando...</span>}
            {error && (
              <span
                className="status-indicator error"
                onClick={checkHealth}
                style={{ cursor: 'pointer' }}
                title="Clic para reintentar conexión"
              >
                ⚠️ Desconectado (Reintentar)
              </span>
            )}
            {health && (
              <span className="status-indicator connected">
                <span className="pulse-dot"></span>
                {health.message}
              </span>
            )}
          </div>
        </div>
      </header>

      {/* Main Body with Left Sidebar + Full Width Content */}
      <div className="app-body">
        {/* Left Sidebar Navigation */}
        <aside className={`app-sidebar ${sidebarCollapsed ? 'collapsed' : ''}`}>
          <div className="sidebar-nav-list">
            {NAV_ITEMS.map((item) => (
              <button
                key={item.id}
                className={`sidebar-nav-item ${activeTab === item.id ? 'active' : ''}`}
                onClick={() => setActiveTab(item.id)}
                title={sidebarCollapsed ? item.label : undefined}
              >
                <span className="nav-item-icon">{item.icon}</span>
                {!sidebarCollapsed && <span className="nav-item-label">{item.label}</span>}
              </button>
            ))}
          </div>
        </aside>

        {/* Content Area */}
        <main className="app-main-content">
          <section className="features-grid-section">
            {activeTab === 'output' && (
              <RiskOutputScreens activeUserId={activeUserId} />
            )}
            {activeTab === 'analytics' && (
              <AnalyticsView />
            )}
            {activeTab === 'alerts' && (
              <AlertsView activeUserId={activeUserId} activeUserName={activeUserName} />
            )}
            {activeTab === 'assistant' && (
              <AssistantView activeUserId={activeUserId} activeUserName={activeUserName} />
            )}
            {activeTab === 'simulator' && (
              <DashboardView activeUserId={activeUserId} activeUserName={activeUserName} />
            )}
            {activeTab === 'profile' && (
              <ProfileView
                activeUserId={activeUserId}
                onProfileUpdated={() => fetchUsers(activeUserId)}
                onProfileDeleted={handleProfileDeleted}
              />
            )}
            {activeTab === 'onboarding' && (
              <OnboardingView onUserCreated={handleUserCreated} />
            )}
          </section>
        </main>
      </div>
    </div>
  );
}

export default App;
