# Guardián Digital

Sistema inteligente de asistencia y monitoreo domiciliario para la detección temprana de emergencias médicas en el hogar, desarrollado como proyecto de fin de ciclo para **UTN-FRBA — Inteligencia Artificial Aplicada a Organizaciones**.

## Problema que resuelve

Personas que viven solas, adultos mayores o personas con condiciones de salud de riesgo pueden sufrir una emergencia médica (caída, ACV, infarto, pérdida de conciencia) sin nadie cerca que note lo ocurrido. Guardián Digital simula un ecosistema de sensores domésticos y biométricos, interpreta síntomas reportados por voz/texto mediante IA, clasifica el nivel de riesgo (Leve / Urgente / Posible Emergencia) y despacha automáticamente la acción correspondiente (recomendación, notificación a contactos, o protocolo de emergencia).

## Arquitectura

- **Backend:** .NET 10, arquitectura Vertical Slice (cada feature es autocontenida: request, handler, endpoint).
- **Base de datos:** SQLite + EF Core.
- **Frontend:** React + TypeScript + Vite.
- **IA:** servicio de interpretación de lenguaje natural (`LanguageModelService`) para evaluar síntomas reportados por el usuario, combinado con reglas clínicas duras y una jerarquía de priorización determinística para la clasificación final de riesgo.
- **Orquestación:** ciclo continuo Observación → Análisis → Decisión → Acción → Registro → Aprendizaje, ejecutado mediante servicios en background (`SensorSimulatorService`, `EventAnalysisService`).

Diagrama de arquitectura completo, diagrama de flujo de agentes y diagramas UML disponibles en el informe del proyecto (`/docs`).

## Cómo correrlo localmente

### Backend
```bash
cd src/GuardianDigital.Api
dotnet restore
dotnet ef database update
dotnet run
```
La API queda disponible en `http://localhost:5000`.

### Frontend
```bash
cd client
npm install
npm run dev
```
El cliente queda disponible en `http://localhost:5173`.

## Estado del proyecto

Prototipo académico funcional. Los sensores biométricos (cámaras, oxímetro, tensiómetro, pulsera) están **simulados** — no hay integración con hardware real. El motor de IA evalúa síntomas informados por el usuario contra un conjunto de escenarios clínicos y reglas de negocio; **no reemplaza diagnóstico médico profesional** — todas las recomendaciones son de carácter orientativo.

## Tests

Suite de tests automatizados end-to-end sobre los flujos principales (onboarding, simulación de sensores, detección de eventos, evaluación de riesgo, despacho de acciones).

## Licencia

MIT
