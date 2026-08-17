import React, { useState } from 'react';
import './Onboarding.css';

interface EmergencyContactForm {
  contactName: string;
  relationship: string;
  phone8: string;
  preferredMethod: string;
}

interface UserProfileResponse {
  id: string;
  fullName: string;
  nationalId: string;
  dateOfBirth: string;
  gender: string;
  primaryPhone: string;
  address: string;
  healthInsurance?: string;
  bloodType: string;
  medicalProfile?: {
    id: string;
    medicalHistory: string;
    currentMedication: string[];
    knownAllergies: string[];
    preexistingConditions: string[];
  };
  emergencyContacts: {
    id: string;
    contactName: string;
    relationship: string;
    phone: string;
    preferredMethod: string;
  }[];
}

interface OnboardingViewProps {
  onUserCreated?: (userId: string) => void;
}

export const OnboardingView: React.FC<OnboardingViewProps> = ({ onUserCreated }) => {
  const [currentStep, setCurrentStep] = useState<number>(1);
  const [loading, setLoading] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [registeredUser, setRegisteredUser] = useState<UserProfileResponse | null>(null);

  // Step 1: Personal Data
  const [fullName, setFullName] = useState<string>('');
  const [nationalId, setNationalId] = useState<string>('');
  const [dateOfBirth, setDateOfBirth] = useState<string>('1960-01-15');
  const [gender, setGender] = useState<string>('Femenino');
  const [primaryPhone8, setPrimaryPhone8] = useState<string>('11443322');
  const [address, setAddress] = useState<string>('');
  const [healthInsurance, setHealthInsurance] = useState<string>('');
  const [bloodType, setBloodType] = useState<string>('O+');

  // Step 2: Clinical History
  const [medicalHistory, setMedicalHistory] = useState<string>('');
  const [medInput, setMedInput] = useState<string>('');
  const [medications, setMedications] = useState<string[]>([]);
  const [allergyInput, setAllergyInput] = useState<string>('');
  const [allergies, setAllergies] = useState<string[]>([]);
  const [conditionInput, setConditionInput] = useState<string>('');
  const [conditions, setConditions] = useState<string[]>([]);

  // Step 3: Emergency Contacts (Pre-seeded with 3 empty contact slots for ease of use)
  const [contacts, setContacts] = useState<EmergencyContactForm[]>([
    { contactName: '', relationship: 'Hija / Hijo', phone8: '11998877', preferredMethod: 'Call' },
    { contactName: '', relationship: 'Familiar / Conviviente', phone8: '11887766', preferredMethod: 'SMS' },
    { contactName: '', relationship: 'Médico de Cabecera', phone8: '11776655', preferredMethod: 'Call' },
  ]);

  const addMedication = () => {
    if (medInput.trim()) {
      setMedications([...medications, medInput.trim()]);
      setMedInput('');
    }
  };

  const removeMedication = (index: number) => {
    setMedications(medications.filter((_, i) => i !== index));
  };

  const addAllergy = () => {
    if (allergyInput.trim()) {
      setAllergies([...allergies, allergyInput.trim()]);
      setAllergyInput('');
    }
  };

  const removeAllergy = (index: number) => {
    setAllergies(allergies.filter((_, i) => i !== index));
  };

  const addCondition = () => {
    if (conditionInput.trim()) {
      setConditions([...conditions, conditionInput.trim()]);
      setConditionInput('');
    }
  };

  const removeCondition = (index: number) => {
    setConditions(conditions.filter((_, i) => i !== index));
  };

  const updateContact = (index: number, field: keyof EmergencyContactForm, value: string) => {
    const updated = [...contacts];
    updated[index][field] = value;
    setContacts(updated);
  };

  const addContactSlot = () => {
    setContacts([
      ...contacts,
      { contactName: '', relationship: 'Familiar', phone8: '', preferredMethod: 'Call' },
    ]);
  };

  const removeContactSlot = (index: number) => {
    if (contacts.length <= 3) {
      setErrorMsg('No es posible eliminar el contacto. La plataforma requiere un mínimo de 3 contactos de emergencia.');
      return;
    }
    setContacts(contacts.filter((_, i) => i !== index));
  };

  const handleNextStep = () => {
    setErrorMsg(null);
    if (currentStep === 1) {
      if (!fullName.trim() || !nationalId.trim() || !address.trim()) {
        setErrorMsg('Por favor complete todos los campos obligatorios (*).');
        return;
      }
      if (nationalId.trim().length > 9) {
        setErrorMsg('El DNI no puede superar los 9 dígitos.');
        return;
      }
      if (primaryPhone8.length !== 8) {
        setErrorMsg('El teléfono principal debe contener exactamente 8 dígitos (ej. 11443322).');
        return;
      }
    }
    setCurrentStep((prev) => Math.min(prev + 1, 3));
  };

  const handlePrevStep = () => {
    setErrorMsg(null);
    setCurrentStep((prev) => Math.max(prev - 1, 1));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (nationalId.trim().length > 9 || nationalId.trim().length === 0) {
      setErrorMsg('El DNI debe contener solo números naturales y máximo 9 dígitos.');
      return;
    }

    if (primaryPhone8.length !== 8) {
      setErrorMsg('El teléfono principal debe contener exactamente 8 dígitos.');
      return;
    }

    if (contacts.length < 3) {
      setErrorMsg('Regla obligatoria: Debe ingresar al menos 3 contactos de emergencia.');
      return;
    }

    for (let i = 0; i < contacts.length; i++) {
      if (!contacts[i].contactName.trim()) {
        setErrorMsg(`El contacto #${i + 1} debe incluir nombre.`);
        return;
      }
      if (contacts[i].phone8.length !== 8) {
        setErrorMsg(`El contacto #${i + 1} debe tener un número de teléfono de exactamente 8 dígitos.`);
        return;
      }
    }

    const payload = {
      fullName: fullName.trim(),
      nationalId: nationalId.trim(),
      dateOfBirth: new Date(dateOfBirth).toISOString(),
      gender,
      primaryPhone: `+549${primaryPhone8.trim()}`,
      address: address.trim(),
      healthInsurance: healthInsurance.trim() || null,
      bloodType,
      medicalProfile: {
        medicalHistory: medicalHistory.trim(),
        currentMedication: medications,
        knownAllergies: allergies,
        preexistingConditions: conditions,
      },
      emergencyContacts: contacts.map((c) => ({
        contactName: c.contactName.trim(),
        relationship: c.relationship.trim(),
        phone: `+549${c.phone8.trim()}`,
        preferredMethod: c.preferredMethod,
      })),
    };

    setLoading(true);

    try {
      // 1. Single transaction POST to backend
      const res = await fetch('http://localhost:5000/api/users', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      const data = await res.json();

      if (!res.ok) {
        throw new Error(data.error || `El registro falló con estado HTTP ${res.status}`);
      }

      // 2. Fetch full profile from backend to verify DB persistence
      const getRes = await fetch(`http://localhost:5000/api/users/${data.id}`);
      if (getRes.ok) {
        const fullProfile = await getRes.json();
        setRegisteredUser(fullProfile);
      } else {
        setRegisteredUser(data);
      }
      onUserCreated?.(data.id);
    } catch (err: any) {
      setErrorMsg(err.message || 'Ocurrió un error inesperado durante el registro.');
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setRegisteredUser(null);
    setCurrentStep(1);
    setFullName('');
    setNationalId('');
    setAddress('');
    setHealthInsurance('');
    setMedicalHistory('');
    setMedications([]);
    setAllergies([]);
    setConditions([]);
    setPrimaryPhone8('11443322');
    setContacts([
      { contactName: '', relationship: 'Hija / Hijo', phone8: '11998877', preferredMethod: 'Call' },
      { contactName: '', relationship: 'Familiar / Conviviente', phone8: '11887766', preferredMethod: 'SMS' },
      { contactName: '', relationship: 'Médico de Cabecera', phone8: '11776655', preferredMethod: 'Call' },
    ]);
  };

  if (registeredUser) {
    return (
      <div className="onboarding-wizard">
        <div className="success-card">
          <div className="success-header">
            <span style={{ fontSize: '3rem' }}>✅</span>
            <div>
              <h2>¡Registro de Paciente Completado con Éxito!</h2>
              <p style={{ color: '#94a3b8', margin: 0, fontSize: '1.1rem' }}>
                Perfil persistido correctamente en la base de datos relacional.
              </p>
            </div>
          </div>

          <div className="profile-detail-grid">
            <div className="detail-block">
              <h4>Identificación Personal</h4>
              <p><strong>Nombre Completo:</strong> {registeredUser.fullName}</p>
              <p><strong>DNI / Documento:</strong> {registeredUser.nationalId}</p>
              <p><strong>Fecha de Nacimiento:</strong> {new Date(registeredUser.dateOfBirth).toLocaleDateString('es-ES')}</p>
              <p><strong>Género:</strong> {registeredUser.gender}</p>
              <p><strong>Teléfono:</strong> {registeredUser.primaryPhone}</p>
              <p><strong>Dirección:</strong> {registeredUser.address}</p>
              <p><strong>Obra Social:</strong> {registeredUser.healthInsurance || 'Particular / Sin Cobertura'}</p>
            </div>

            <div className="detail-block">
              <h4>Ficha Médica & Rescate</h4>
              <p><strong>Grupo Sanguíneo:</strong> <span className="blood-badge">{registeredUser.bloodType}</span></p>
              <p><strong>Historial / Antecedentes:</strong> {registeredUser.medicalProfile?.medicalHistory || 'Ninguno registrado'}</p>
              
              <div style={{ marginTop: '0.5rem' }}>
                <strong>Alergias Conocidas:</strong>
                <div className="tag-list" style={{ marginTop: '0.25rem' }}>
                  {registeredUser.medicalProfile?.knownAllergies?.map((a, i) => (
                    <span key={i} className="tag-chip" style={{ borderColor: '#ef4444', color: '#f87171' }}>
                      ⛔ {a}
                    </span>
                  ))}
                  {(!registeredUser.medicalProfile?.knownAllergies || registeredUser.medicalProfile.knownAllergies.length === 0) && (
                    <span style={{ color: '#94a3b8', fontSize: '0.9rem' }}>Sin alergias reportadas</span>
                  )}
                </div>
              </div>

              <div style={{ marginTop: '0.5rem' }}>
                <strong>Medicación Habitual:</strong>
                <div className="tag-list" style={{ marginTop: '0.25rem' }}>
                  {registeredUser.medicalProfile?.currentMedication?.map((m, i) => (
                    <span key={i} className="tag-chip">💊 {m}</span>
                  ))}
                  {(!registeredUser.medicalProfile?.currentMedication || registeredUser.medicalProfile.currentMedication.length === 0) && (
                    <span style={{ color: '#94a3b8', fontSize: '0.9rem' }}>Sin medicación regular</span>
                  )}
                </div>
              </div>
            </div>

            <div className="detail-block full-width">
              <h4>Contactos de Emergencia Vinculados ({registeredUser.emergencyContacts.length})</h4>
              <div className="contacts-preview-grid">
                {registeredUser.emergencyContacts.map((c, i) => (
                  <div key={c.id || i} className="contact-preview-card">
                    <div style={{ fontWeight: 'bold', fontSize: '1.05rem', color: '#38bdf8' }}>{c.contactName}</div>
                    <div style={{ color: '#cbd5e1' }}>{c.relationship}</div>
                    <div style={{ color: '#94a3b8', fontSize: '0.9rem' }}>📞 {c.phone}</div>
                    <div style={{ fontSize: '0.8rem', color: '#4ade80', marginTop: '0.25rem' }}>
                      Canal: {c.preferredMethod === 'Call' ? 'Llamada' : c.preferredMethod === 'SMS' ? 'SMS' : 'Notificación Push'}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div style={{ marginTop: '2rem', display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
            <button className="btn-large btn-primary" onClick={handleReset}>
              ➕ Registrar Otro Paciente
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="onboarding-wizard">
      <div className="wizard-title">
        <span style={{ fontSize: '2.5rem' }}>📝</span>
        <h2>Registro de Nuevo Paciente</h2>
      </div>
      <div className="wizard-subtitle">
        Registro accesible paso a paso de datos personales, ficha clínica y red de contactos de emergencia.
      </div>

      {/* Stepper Header */}
      <div className="stepper-header">
        <div className={`step-item ${currentStep === 1 ? 'active' : currentStep > 1 ? 'completed' : ''}`}>
          <div className="step-badge">{currentStep > 1 ? '✓' : '1'}</div>
          <div className="step-label">Datos Personales</div>
        </div>
        <div className={`step-item ${currentStep === 2 ? 'active' : currentStep > 2 ? 'completed' : ''}`}>
          <div className="step-badge">{currentStep > 2 ? '✓' : '2'}</div>
          <div className="step-label">Historial Clínico</div>
        </div>
        <div className={`step-item ${currentStep === 3 ? 'active' : ''}`}>
          <div className="step-badge">3</div>
          <div className="step-label">Contactos de Emergencia</div>
        </div>
      </div>

      {errorMsg && <div className="error-banner">⚠️ {errorMsg}</div>}

      <form onSubmit={handleSubmit}>
        {/* STEP 1: Personal Data */}
        {currentStep === 1 && (
          <div className="form-grid">
            <div className="form-group">
              <label htmlFor="fullName">Nombre Completo *</label>
              <input
                id="fullName"
                type="text"
                className="form-control"
                placeholder="ej. Elena Vásquez"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="nationalId">Documento de Identidad (DNI) * (Máx. 9 dígitos)</label>
              <input
                id="nationalId"
                type="text"
                inputMode="numeric"
                maxLength={9}
                className="form-control"
                placeholder="ej. 30998877"
                value={nationalId}
                onChange={(e) => setNationalId(e.target.value.replace(/\D/g, '').slice(0, 9))}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="dateOfBirth">Fecha de Nacimiento</label>
              <input
                id="dateOfBirth"
                type="date"
                className="form-control"
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label htmlFor="gender">Género</label>
              <select
                id="gender"
                className="form-control"
                value={gender}
                onChange={(e) => setGender(e.target.value)}
              >
                <option value="Femenino">Femenino</option>
                <option value="Masculino">Masculino</option>
                <option value="Otro">Otro</option>
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="primaryPhone">Teléfono Principal * (8 dígitos)</label>
              <div className="phone-input-wrapper">
                <span className="phone-prefix-badge">+549</span>
                <input
                  id="primaryPhone"
                  type="text"
                  inputMode="numeric"
                  maxLength={8}
                  className="form-control"
                  placeholder="11443322"
                  value={primaryPhone8}
                  onChange={(e) => setPrimaryPhone8(e.target.value.replace(/\D/g, '').slice(0, 8))}
                  required
                />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="bloodType">Grupo Sanguíneo (Ficha de Rescate)</label>
              <select
                id="bloodType"
                className="form-control"
                value={bloodType}
                onChange={(e) => setBloodType(e.target.value)}
              >
                <option value="A+">A+</option>
                <option value="A-">A-</option>
                <option value="B+">B+</option>
                <option value="B-">B-</option>
                <option value="AB+">AB+</option>
                <option value="AB-">AB-</option>
                <option value="O+">O+</option>
                <option value="O-">O-</option>
              </select>
            </div>

            <div className="form-group full-width">
              <label htmlFor="address">Dirección Domiciliaria *</label>
              <input
                id="address"
                type="text"
                className="form-control"
                placeholder="Calle, número, piso, departamento..."
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                required
              />
            </div>

            <div className="form-group full-width">
              <label htmlFor="healthInsurance">Obra Social / Cobertura Médica</label>
              <input
                id="healthInsurance"
                type="text"
                className="form-control"
                placeholder="ej. OSDE 410, Swiss Medical, PAMI"
                value={healthInsurance}
                onChange={(e) => setHealthInsurance(e.target.value)}
              />
            </div>
          </div>
        )}

        {/* STEP 2: Clinical History */}
        {currentStep === 2 && (
          <div className="form-grid">
            <div className="form-group full-width">
              <label htmlFor="medicalHistory">Historial Clínico / Antecedentes</label>
              <textarea
                id="medicalHistory"
                className="form-control"
                rows={3}
                placeholder="Antecedentes médicos relevantes, cirugías o condiciones diagnosticadas..."
                value={medicalHistory}
                onChange={(e) => setMedicalHistory(e.target.value)}
              />
            </div>

            <div className="form-group full-width">
              <label>Medicación Habitual</label>
              <div className="tag-input-row">
                <input
                  type="text"
                  className="form-control"
                  style={{ flex: 1 }}
                  placeholder="ej. Losartán 50mg diario, Enalapril 10mg..."
                  value={medInput}
                  onChange={(e) => setMedInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), addMedication())}
                />
                <button type="button" className="add-tag-btn" onClick={addMedication}>
                  Agregar
                </button>
              </div>
              <div className="tag-list">
                {medications.map((m, idx) => (
                  <span key={idx} className="tag-chip">
                    💊 {m}
                    <button type="button" className="remove-tag" onClick={() => removeMedication(idx)}>
                      ×
                    </button>
                  </span>
                ))}
              </div>
            </div>

            <div className="form-group full-width">
              <label>Alergias Conocidas (Ficha de Rescate)</label>
              <div className="tag-input-row">
                <input
                  type="text"
                  className="form-control"
                  style={{ flex: 1 }}
                  placeholder="ej. Penicilina, Sulfamidas, Látex..."
                  value={allergyInput}
                  onChange={(e) => setAllergyInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), addAllergy())}
                />
                <button type="button" className="add-tag-btn" onClick={addAllergy}>
                  Agregar
                </button>
              </div>
              <div className="tag-list">
                {allergies.map((a, idx) => (
                  <span key={idx} className="tag-chip" style={{ borderColor: '#ef4444', color: '#f87171' }}>
                    ⚠️ {a}
                    <button type="button" className="remove-tag" onClick={() => removeAllergy(idx)}>
                      ×
                    </button>
                  </span>
                ))}
              </div>
            </div>

            <div className="form-group full-width">
              <label>Enfermedades Preexistentes</label>
              <div className="tag-input-row">
                <input
                  type="text"
                  className="form-control"
                  style={{ flex: 1 }}
                  placeholder="ej. Hipertensión Arterial, Osteoporosis, Diabetes Tipo 2"
                  value={conditionInput}
                  onChange={(e) => setConditionInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), addCondition())}
                />
                <button type="button" className="add-tag-btn" onClick={addCondition}>
                  Agregar
                </button>
              </div>
              <div className="tag-list">
                {conditions.map((c, idx) => (
                  <span key={idx} className="tag-chip" style={{ borderColor: '#eab308', color: '#facc15' }}>
                    🏥 {c}
                    <button type="button" className="remove-tag" onClick={() => removeCondition(idx)}>
                      ×
                    </button>
                  </span>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* STEP 3: Emergency Contacts */}
        {currentStep === 3 && (
          <div>
            <div className={`contacts-badge ${contacts.length >= 3 ? 'valid' : ''}`}>
              {contacts.length >= 3
                ? `✓ ${contacts.length} Contactos de Emergencia Configurados (Regla cumplida: Mínimo 3)`
                : `⚠️ ${contacts.length} de 3 contactos mínimos requeridos`}
            </div>

            {contacts.map((contact, idx) => (
              <div key={idx} className="contact-card">
                <div className="contact-card-header">
                  <h4 style={{ margin: 0, fontSize: '1.2rem', color: '#38bdf8' }}>
                    Contacto de Emergencia #{idx + 1}
                  </h4>
                  {contacts.length > 3 && (
                    <button
                      type="button"
                      className="btn-danger-outline"
                      onClick={() => removeContactSlot(idx)}
                    >
                      Eliminar
                    </button>
                  )}
                </div>

                <div className="form-grid">
                  <div className="form-group">
                    <label>Nombre Completo *</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="ej. Sofia Vásquez"
                      value={contact.contactName}
                      onChange={(e) => updateContact(idx, 'contactName', e.target.value)}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Parentesco / Vínculo *</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="ej. Hija, Hijo, Vecino, Médico"
                      value={contact.relationship}
                      onChange={(e) => updateContact(idx, 'relationship', e.target.value)}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Número de Teléfono * (8 dígitos)</label>
                    <div className="phone-input-wrapper">
                      <span className="phone-prefix-badge">+549</span>
                      <input
                        type="text"
                        inputMode="numeric"
                        maxLength={8}
                        className="form-control"
                        placeholder="11998877"
                        value={contact.phone8}
                        onChange={(e) => updateContact(idx, 'phone8', e.target.value.replace(/\D/g, '').slice(0, 8))}
                        required
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label>Método Preferido de Alerta</label>
                    <select
                      className="form-control"
                      value={contact.preferredMethod}
                      onChange={(e) => updateContact(idx, 'preferredMethod', e.target.value)}
                    >
                      <option value="Call">Llamada Telefónica</option>
                      <option value="SMS">Mensaje de Texto SMS</option>
                      <option value="PushNotification">Notificación Push</option>
                    </select>
                  </div>
                </div>
              </div>
            ))}

            <button
              type="button"
              className="btn-large btn-secondary"
              style={{ width: '100%', marginBottom: '1rem' }}
              onClick={addContactSlot}
            >
              + Agregar Otro Contacto de Emergencia
            </button>
          </div>
        )}

        {/* Wizard Controls */}
        <div className="wizard-actions">
          {currentStep > 1 ? (
            <button type="button" className="btn-large btn-secondary" onClick={handlePrevStep}>
              ← Paso Anterior
            </button>
          ) : (
            <div></div>
          )}

          {currentStep < 3 ? (
            <button type="button" className="btn-large btn-primary" onClick={handleNextStep}>
              Siguiente Paso →
            </button>
          ) : (
            <button type="submit" className="btn-large btn-success" disabled={loading}>
              {loading ? 'Guardando en Base de Datos...' : '✓ Finalizar y Crear Usuario'}
            </button>
          )}
        </div>
      </form>
    </div>
  );
};
