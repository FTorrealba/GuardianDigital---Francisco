/**
 * Centralized API Configuration for Guardián Digital
 * 
 * Dynamically resolves the API base URL:
 * - Local Development: uses VITE_API_URL or defaults to http://localhost:5000
 * - Production (Vercel): uses VITE_API_URL (https://guardian-digital-api.onrender.com)
 */
export const API_BASE_URL: string =
  (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/+$/, '') ||
  'http://localhost:5000';
