import React, { useEffect, useState } from 'react';
import './Profile.css';

interface EmergencyContactForm {
  id?: string;
  contactName: string;
  relationship: string;
  phone10: string;
  preferredMethod: string;
}

interface UserProfileData {
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

interface ProfileViewProps {
  activeUserId?: string;
  onProfileUpdated?: () => void;
  onProfileDeleted?: () => void;
}

export const ProfileView: React.FC<ProfileViewProps> = ({
  activeUserId,
  onProfileUpdated,
  onProfileDeleted,
}) => {
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [deleting, setDeleting] = useState<boolean>(false);
  const [showDeleteModal, setShowDeleteModal] = useState<boolean>(false);
  const [saveSuccess, setSaveSuccess] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Personal Info State
  const [fullName, setFullName] = useState<string>('');
  const [nationalId, setNationalId] = useState<string>('');
  const [dateOfBirth, setDateOfBirth] = useState<string>('1960-01-15');
  const [gender, setGender] = useState<string>('Femenino');
  const [primaryPhone10, setPrimaryPhone10] = useState<string>('');
  const [address, setAddress] = useState<string>('');
  const [healthInsurance, setHealthInsurance] = useState<string>('');
  const [bloodType, setBloodType] = useState<string>('O+');

  // Clinical Profile State
  const [medicalHistory, setMedicalHistory] = useState<string>('');
  const [medInput, setMedInput] = useState<string>('');
  const [medications, setMedications] = useState<string[]>([]);
  const [allergyInput, setAllergyInput] = useState<string>('');
  const [allergies, setAllergies] = useState<string[]>([]);
  const [conditionInput, setConditionInput] = useState<string>('');
  const [conditions, setConditions] = useState<string[]>([]);

  // Emergency Contacts State
  const [contacts, setContacts] = useState<EmergencyContactForm[]>([]);

  const extract10Digits = (phoneStr: string) => {
    const digits = (phoneStr || '').replace(/\D/g, '');
    if (digits.startsWith('549') && digits.length >= 13) {
      return digits.slice(3, 13);
    }
    return digits.slice(-10);
  };

  const fetchUserData = async () => {
    if (!activeUserId) return;
    setLoading(true);
    setErrorMsg(null);
    setSaveSuccess(false);

    try {
      const res = await fetch(`http://localhost:5000/api/users/${activeUserId}`);
      if (res.ok) {
        const user: UserProfileData = await res.json();
        setFullName(user.fullName);
        setNationalId(user.nationalId.replace(/\D/g, '').slice(0, 9));
        setDateOfBirth(user.dateOfBirth ? user.dateOfBirth.split('T')[0] : '1960-01-15');
        setGender(user.gender || 'Femenino');
        setPrimaryPhone10(extract10Digits(user.primaryPhone));
        setAddress(user.address);
        setHealthInsurance(user.healthInsurance || '');
        setBloodType(user.bloodType || 'O+');

        if (user.medicalProfile) {
          setMedicalHistory(user.medicalProfile.medicalHistory || '');
          setMedications(user.medicalProfile.currentMedication || []);
          setAllergies(user.medicalProfile.knownAllergies || []);
          setConditions(user.medicalProfile.preexistingConditions || []);
        } else {
          setMedicalHistory('');
          setMedications([]);
          setAllergies([]);
          setConditions([]);
        }

        setContacts(
          (user.emergencyContacts || []).map((c) => ({
            id: c.id,
            contactName: c.contactName,
            relationship: c.relationship,
            phone10: extract10Digits(c.phone),
            preferredMethod: c.preferredMethod || 'Call',
          }))
        );
      } else {
        setErrorMsg('No se pudo cargar la información del usuario.');
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Error de conexión.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchUserData();
  }, [activeUserId]);

  const addMedication = () => {
    if (medInput.trim() && !medications.includes(medInput.trim())) {
      setMedications([...medications, medInput.trim()]);
      setMedInput('');
    }
  };

  const removeMedication = (index: number) => {
    setMedications(medications.filter((_, i) => i !== index));
  };

  const addAllergy = () => {
    if (allergyInput.trim() && !allergies.includes(allergyInput.trim())) {
      setAllergies([...allergies, allergyInput.trim()]);
      setAllergyInput('');
    }
  };

  const removeAllergy = (index: number) => {
    setAllergies(allergies.filter((_, i) => i !== index));
  };

  const addCondition = () => {
    if (conditionInput.trim() && !conditions.includes(conditionInput.trim())) {
      setConditions([...conditions, conditionInput.trim()]);
      setConditionInput('');
    }
  };

  const removeCondition = (index: number) => {
    setConditions(conditions.filter((_, i) => i !== index));
  };

  const updateContact = (index: number, field: keyof EmergencyContactForm, value: string) => {
    const updated = [...contacts];
    updated[index] = { ...updated[index], [field]: value };
    setContacts(updated);
  };

  const addContactSlot = () => {
    setContacts([
      ...contacts,
      { contactName: '', relationship: 'Familiar', phone10: '1199887766', preferredMethod: 'Call' },
    ]);
  };

  const removeContactSlot = (index: number) => {
    if (contacts.length <= 3) {
      setErrorMsg('No es posible eliminar el contacto. El sistema requiere un mínimo de 3 contactos de emergencia.');
      return;
    }
    setContacts(contacts.filter((_, i) => i !== index));
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeUserId) return;
    setErrorMsg(null);
    setSaveSuccess(false);

    if (nationalId.trim().length > 9 || nationalId.trim().length === 0) {
      setErrorMsg('El DNI debe contener solo números naturales y máximo 9 dígitos.');
      return;
    }

    if (primaryPhone10.length !== 10) {
      setErrorMsg('El teléfono principal debe contener exactamente 10 dígitos numéricos (ej. 1144332211).');
      return;
    }

    if (contacts.length < 3) {
      setErrorMsg('Debe registrar al menos 3 contactos de emergencia.');
      return;
    }

    for (let i = 0; i < contacts.length; i++) {
      if (!contacts[i].contactName.trim()) {
        setErrorMsg(`El contacto de emergencia #${i + 1} debe incluir nombre.`);
        return;
      }
      if (contacts[i].phone10.length !== 10) {
        setErrorMsg(`El contacto de emergencia #${i + 1} debe contener un teléfono de exactamente 10 dígitos (ej. 1199887766).`);
        return;
      }
    }

    const payload = {
      fullName: fullName.trim(),
      nationalId: nationalId.trim(),
      dateOfBirth: new Date(dateOfBirth).toISOString(),
      gender,
      primaryPhone: `+549${primaryPhone10.trim()}`,
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
        phone: `+549${c.phone10.trim()}`,
        preferredMethod: c.preferredMethod,
      })),
    };

    setSaving(true);
    try {
      const res = await fetch(`http://localhost:5000/api/users/${activeUserId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || 'Error al actualizar el perfil.');
      }

      setSaveSuccess(true);
      onProfileUpdated?.();
    } catch (err: any) {
      setErrorMsg(err.message || 'Error al guardar los cambios.');
    } finally {
      setSaving(false);
    }
  };

  const handleExecuteDelete = async () => {
    if (!activeUserId) return;
    setDeleting(true);
    setErrorMsg(null);

    try {
      const res = await fetch(`http://localhost:5000/api/users/${activeUserId}`, {
        method: 'DELETE',
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.error || 'Error al eliminar el perfil del paciente.');
      }

      setShowDeleteModal(false);
      onProfileDeleted?.();
    } catch (err: any) {
      setErrorMsg(err.message || 'Error al procesar la eliminación.');
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="profile-container">
        <div className="profile-card" style={{ textAlign: 'center', padding: '3rem' }}>
          <p style={{ color: '#94a3b8', fontSize: '1.2rem' }}>Cargando perfil clínico del paciente...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="profile-container">
      {/* In-App Confirmation Modal */}
      {showDeleteModal && (
        <div className="modal-overlay" onClick={() => setShowDeleteModal(false)}>
          <div className="delete-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="delete-modal-header">
              <span style={{ fontSize: '2rem' }}>⚠️</span>
              <h3>Confirmar Eliminación de Paciente</h3>
            </div>
            <div className="delete-modal-body">
              <p style={{ margin: '0 0 0.5rem 0' }}>
                ¿Está seguro de que desea eliminar permanentemente al paciente{' '}
                <strong style={{ color: '#f87171' }}>"{fullName}"</strong> (DNI: {nationalId})?
              </p>
              <p style={{ margin: 0, fontSize: '0.88rem', color: '#94a3b8' }}>
                Esta acción eliminará de forma irreversible su ficha clínica, contactos de emergencia vinculados, dispositivos e historial de incidentes de la base de datos.
              </p>
            </div>
            <div className="delete-modal-actions">
              <button
                type="button"
                className="btn-modal-cancel"
                onClick={() => setShowDeleteModal(false)}
                disabled={deleting}
              >
                ✕ Cancelar
              </button>
              <button
                type="button"
                className="btn-modal-confirm"
                onClick={handleExecuteDelete}
                disabled={deleting}
              >
                {deleting ? 'Eliminando de la BD...' : '🗑️ Sí, Eliminar Definitivamente'}
              </button>
            </div>
          </div>
        </div>
      )}

      <form className="profile-card" onSubmit={handleSaveProfile}>
        <div className="profile-header">
          <div>
            <h2>👤 Perfil Clínico & Datos de Rescate</h2>
            <p style={{ color: '#94a3b8', margin: 0 }}>
              Edita información de contacto, cobertura médica, ficha clínica y gestión de contactos de emergencia.
            </p>
          </div>
          {saveSuccess && (
            <div className="save-success-toast">
              <span>✓</span> Cambios guardados correctamente
            </div>
          )}
        </div>

        {errorMsg && (
          <div style={{ background: 'rgba(239, 68, 68, 0.15)', border: '1px solid #ef4444', color: '#fca5a5', padding: '0.75rem 1.25rem', borderRadius: '10px', marginBottom: '1.5rem' }}>
            ⚠️ {errorMsg}
          </div>
        )}

        {/* 1. Datos Personales & Domicilio */}
        <div className="profile-section-title">
          <span>📋</span> Datos Personales & Domicilio
        </div>

        <div className="form-grid-2">
          <div className="form-field">
            <label>Nombre Completo *</label>
            <input
              type="text"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label>Documento de Identidad (DNI) * (Máx. 9 dígitos)</label>
            <input
              type="text"
              inputMode="numeric"
              maxLength={9}
              value={nationalId}
              onChange={(e) => setNationalId(e.target.value.replace(/\D/g, '').slice(0, 9))}
              required
            />
          </div>

          <div className="form-field">
            <label>Fecha de Nacimiento</label>
            <input
              type="date"
              value={dateOfBirth}
              onChange={(e) => setDateOfBirth(e.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label>Género</label>
            <select value={gender} onChange={(e) => setGender(e.target.value)}>
              <option value="Femenino">Femenino</option>
              <option value="Masculino">Masculino</option>
              <option value="Otro">Otro</option>
            </select>
          </div>

          <div className="form-field">
            <label>Teléfono Principal de Contacto * (10 dígitos)</label>
            <div className="phone-input-wrapper">
              <span className="phone-prefix-badge">+549</span>
              <input
                type="text"
                inputMode="numeric"
                maxLength={10}
                value={primaryPhone10}
                onChange={(e) => setPrimaryPhone10(e.target.value.replace(/\D/g, '').slice(0, 10))}
                placeholder="1144332211"
                required
              />
            </div>
          </div>

          <div className="form-field">
            <label>Grupo Sanguíneo (Ficha de Rescate) *</label>
            <select value={bloodType} onChange={(e) => setBloodType(e.target.value)}>
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
        </div>

        <div className="form-grid-2">
          <div className="form-field">
            <label>Dirección Domiciliaria Completa *</label>
            <input
              type="text"
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              placeholder="Calle, número, piso, depto, ciudad..."
              required
            />
          </div>

          <div className="form-field">
            <label>Obra Social / Cobertura Médica</label>
            <input
              type="text"
              value={healthInsurance}
              onChange={(e) => setHealthInsurance(e.target.value)}
              placeholder="ej. OSDE 410, Swiss Medical, PAMI..."
            />
          </div>
        </div>

        {/* 2. Historial Clínico & Alergias */}
        <div className="profile-section-title">
          <span>🩺</span> Historial Clínico & Ficha Médica
        </div>

        <div className="form-field" style={{ marginBottom: '1.25rem' }}>
          <label>Antecedentes Médicos Generales</label>
          <textarea
            rows={2}
            value={medicalHistory}
            onChange={(e) => setMedicalHistory(e.target.value)}
            placeholder="Cirugías previas, condiciones crónicas o notas relevantes para el paramédico..."
          />
        </div>

        <div className="form-grid-2">
          {/* Medicación Habitual */}
          <div className="form-field">
            <label>Medicación Habitual</label>
            <div className="chip-input-group">
              <input
                type="text"
                placeholder="ej. Losartán 50mg, Aspirina..."
                value={medInput}
                onChange={(e) => setMedInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addMedication();
                  }
                }}
              />
              <button type="button" className="chip-add-btn" onClick={addMedication}>
                + Agregar
              </button>
            </div>
            <div className="chips-container">
              {medications.map((m, idx) => (
                <span key={idx} className="chip-tag">
                  💊 {m}
                  <button type="button" className="chip-remove-btn" onClick={() => removeMedication(idx)}>
                    ×
                  </button>
                </span>
              ))}
            </div>
          </div>

          {/* Alergias Conocidas */}
          <div className="form-field">
            <label>Alergias Conocidas (Ficha de Rescate)</label>
            <div className="chip-input-group">
              <input
                type="text"
                placeholder="ej. Penicilina, Ibuprofeno..."
                value={allergyInput}
                onChange={(e) => setAllergyInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addAllergy();
                  }
                }}
              />
              <button type="button" className="chip-add-btn" onClick={addAllergy}>
                + Agregar
              </button>
            </div>
            <div className="chips-container">
              {allergies.map((a, idx) => (
                <span key={idx} className="chip-tag allergy">
                  ⛔ {a}
                  <button type="button" className="chip-remove-btn" onClick={() => removeAllergy(idx)}>
                    ×
                  </button>
                </span>
              ))}
            </div>
          </div>
        </div>

        <div className="form-field" style={{ marginBottom: '1.5rem' }}>
          <label>Enfermedades Preexistentes</label>
          <div className="chip-input-group">
            <input
              type="text"
              placeholder="ej. Hipertensión Arterial, Diabetes Tipo 2, Osteoporosis..."
              value={conditionInput}
              onChange={(e) => setConditionInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  addCondition();
                }
              }}
            />
            <button type="button" className="chip-add-btn" onClick={addCondition}>
              + Agregar
            </button>
          </div>
          <div className="chips-container">
            {conditions.map((c, idx) => (
              <span key={idx} className="chip-tag condition">
                ⚠️ {c}
                <button type="button" className="chip-remove-btn" onClick={() => removeCondition(idx)}>
                  ×
                </button>
              </span>
            ))}
          </div>
        </div>

        {/* 3. Contactos de Emergencia */}
        <div className="profile-section-title">
          <span>📲</span> Red de Contactos de Emergencia (Mínimo 3 Requeridos)
        </div>

        <div className="contacts-editor-list">
          {contacts.map((c, idx) => (
            <div key={idx} className="contact-edit-card">
              <div className="contact-edit-header">
                <span style={{ fontWeight: 800, color: '#38bdf8' }}>
                  Contacto #{idx + 1}
                </span>
                <button
                  type="button"
                  className="btn-remove-contact"
                  onClick={() => removeContactSlot(idx)}
                  disabled={contacts.length <= 3}
                  title={contacts.length <= 3 ? 'Se requieren mínimo 3 contactos' : 'Eliminar contacto'}
                >
                  ✕ Eliminar Contacto
                </button>
              </div>

              <div className="form-grid-2">
                <div className="form-field">
                  <label>Nombre y Apellido *</label>
                  <input
                    type="text"
                    value={c.contactName}
                    onChange={(e) => updateContact(idx, 'contactName', e.target.value)}
                    placeholder="ej. Sofía Vásquez"
                    required
                  />
                </div>

                <div className="form-field">
                  <label>Parentesco / Vínculo *</label>
                  <input
                    type="text"
                    value={c.relationship}
                    onChange={(e) => updateContact(idx, 'relationship', e.target.value)}
                    placeholder="ej. Hija, Conviviente, Médico..."
                    required
                  />
                </div>

                <div className="form-field">
                  <label>Teléfono Directo * (10 dígitos)</label>
                  <div className="phone-input-wrapper">
                    <span className="phone-prefix-badge">+549</span>
                    <input
                      type="text"
                      inputMode="numeric"
                      maxLength={10}
                      value={c.phone10}
                      onChange={(e) => updateContact(idx, 'phone10', e.target.value.replace(/\D/g, '').slice(0, 10))}
                      placeholder="1199887766"
                      required
                    />
                  </div>
                </div>

                <div className="form-field">
                  <label>Canal Preferido de Notificación</label>
                  <select
                    value={c.preferredMethod}
                    onChange={(e) => updateContact(idx, 'preferredMethod', e.target.value)}
                  >
                    <option value="Call">Llamada Telefónica</option>
                    <option value="SMS">Mensaje de Texto (SMS)</option>
                    <option value="Push">Notificación Push</option>
                  </select>
                </div>
              </div>
            </div>
          ))}

          <button type="button" className="btn-add-contact" onClick={addContactSlot}>
            + Agregar Nuevo Contacto de Emergencia
          </button>
        </div>

        {/* Guardar Cambios */}
        <div className="profile-actions-bar">
          <button type="submit" className="btn-save-profile" disabled={saving}>
            {saving ? 'Guardando cambios...' : '💾 Guardar Cambios del Perfil'}
          </button>
        </div>

        {/* Zona de Peligro: Eliminar Perfil */}
        <div className="profile-danger-zone">
          <div className="danger-zone-text">
            <span style={{ fontWeight: 800, color: '#f87171', fontSize: '1.05rem' }}>
              ⚠️ Zona de Peligro: Eliminar Paciente
            </span>
            <small style={{ color: '#94a3b8', fontSize: '0.9rem' }}>
              Elimina permanentemente este paciente, su ficha médica y todos sus registros asociados.
            </small>
          </div>
          <button
            type="button"
            className="btn-delete-profile"
            onClick={() => setShowDeleteModal(true)}
            disabled={deleting}
          >
            🗑️ Eliminar Perfil del Paciente
          </button>
        </div>
      </form>
    </div>
  );
};
