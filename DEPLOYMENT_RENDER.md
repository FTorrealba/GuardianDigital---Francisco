# 🚀 Guía de Despliegue en Render.com - Guardián Digital API

Este documento detalla la configuración y los pasos necesarios para desplegar la API de **Guardián Digital** (.NET 10) en **Render.com** utilizando contenedores Docker y PostgreSQL administrado.

---

## 1. Arquitectura de Despliegue

- **Runtime**: .NET 10 (ASP.NET Core Web API).
- **Contenedor**: Dockerfile multi-stage (`mcr.microsoft.com/dotnet/sdk:10.0` y `mcr.microsoft.com/dotnet/aspnet:10.0`).
- **Base de Datos**: PostgreSQL administrado en Render (producción) y SQLite (desarrollo local / tests).
- **Puerto de Escucha**: Configurado dinámicamente mediante la variable `$PORT` provista por Render (`0.0.0.0:${PORT}`).
- **Health Check**: Endpoint `/health` expuesto para monitoreo y verificación de estado.
- **CORS**: Habilitado dinámicamente para entornos locales, Vercel (`*.vercel.app`), Netlify (`*.netlify.app`) y dominios personalizados.

---

## 2. Paso a Paso para Desplegar en Render

### Paso 1: Crear la Base de Datos PostgreSQL
1. En el panel de Render, haz clic en **New +** ➔ **PostgreSQL**.
2. Completa los campos:
   - **Name**: `guardian-digital-db`
   - **Database**: `guardian_db`
   - **User**: `guardian_user`
   - **Region**: Selecciona la misma región donde desplegarás el Web Service (ej. *Oregon* o *Frankfurt*).
3. Haz clic en **Create Database**.
4. Una vez creada, copia la **Internal Database URL** (o vincula la base de datos directamente al Web Service).

---

### Paso 2: Crear el Web Service en Render
1. Haz clic en **New +** ➔ **Web Service**.
2. Conecta el repositorio de GitHub: `https://github.com/FTorrealba/GuardianDigital---Francisco`.
3. Configuración del servicio:
   - **Name**: `guardian-digital-api`
   - **Language / Environment**: `Docker`
   - **Branch**: `main`
   - **Dockerfile Path**: `./Dockerfile`
   - **Docker Context**: `.`
   - **Health Check Path**: `/health`

---

### Paso 3: Configurar Variables de Entorno (Environment Variables)

En la sección **Environment** del Web Service en Render, añade las siguientes variables:

| Variable | Valor | Descripción |
| :--- | :--- | :--- |
| `DATABASE_URL` | *`postgres://usuario:password@dpg-xxxx:5432/guardian_db`* | Cadena de conexión interna de PostgreSQL. Si vinculas la base de datos desde Render, se inyecta automáticamente. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Activa el entorno de producción en ASP.NET Core. |
| `Cors__AllowedOrigins__0` | `https://guardian-digital.vercel.app` | URL de producción del frontend en Vercel. |
| `Cors__AllowedOrigins__1` | `https://guardian-digital.netlify.app` | URL de producción del frontend en Netlify. |

> **Nota**: Render inyecta automáticamente la variable `PORT` al iniciar el contenedor, la cual es leída por la aplicación en `Program.cs`.

---

## 3. Inicialización Automática del Esquema

Al iniciar la API en producción:
- EF Core detecta la conexión a PostgreSQL y ejecuta `db.Database.EnsureCreated()`.
- Se generan automáticamente todas las tablas, relaciones, claves foráneas e índices:
  - `Users`
  - `MedicalProfiles`
  - `EmergencyContacts`
  - `LinkedDevices`
  - `Incidents`
  - `UserResponses`
  - `ActionsExecuted`
  - `SensorReadings`
  - `SystemHealthChecks`

---

## 4. Endpoints de Verificación

Una vez desplegado el servicio, puedes verificar el estado del backend accediendo a:

```http
GET https://<tu-servicio-render>.onrender.com/health
```

Respuesta esperada:
```json
{
  "status": "Healthy",
  "message": "Guardián Digital API is operational",
  "timestamp": "2026-08-17T15:51:05.8656566Z",
  "databaseRecordCount": 0
}
```

---

## 5. Comandos de Verificación Local

```bash
# Compilar y ejecutar pruebas
dotnet restore
dotnet build
dotnet test

# Construir imagen Docker localmente
docker build -t guardian-digital-api .

# Ejecutar contenedor local
docker run --rm -p 8080:8080 -e PORT=8080 guardian-digital-api
```
