import axios from 'axios';

// Resolves which origin to call the API on. Three cases, controlled by
// public/config.js's apiBaseUrl (edit that file on the server - no rebuild):
//   - unset/null (default): same hostname/IP this page was loaded from, on
//     the API's default port 5000 - this is the common IIS setup, the
//     frontend and API as two separate sites/ports on the same server, and
//     needs no configuration at all, including when the hostname/IP changes.
//   - '' (explicit empty string): same-origin, no port override - use this
//     when the API is merged into the same site/port as this app (it serves
//     both the SPA and /api/*, see backend Program.cs).
//   - any other string: used as-is (API hosted on a different host entirely).
function resolveApiOrigin() {
  const configured = window.__APP_CONFIG__?.apiBaseUrl;

  if (configured === '') return '';
  if (configured) return configured.replace(/\/$/, '');

  return `${window.location.protocol}//${window.location.hostname}:5000`;
}

const configuredOrigin = resolveApiOrigin();

export const api = axios.create({
  baseURL: `${configuredOrigin}/api`,
  timeout: 30000
});

// Builds a browser-loadable URL for a root-relative path the API returned
// (e.g. an uploaded logo's "/uploads/..." path).
export function assetUrl(path) {
  if (!path) return '';
  if (/^https?:\/\//i.test(path)) return path;
  return `${configuredOrigin}${path}`;
}

api.interceptors.request.use(config => {
  const token = sessionStorage.getItem('token');

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      sessionStorage.removeItem('token');
      sessionStorage.removeItem('permissions');
      sessionStorage.removeItem('displayName');
      sessionStorage.removeItem('designation');
      window.dispatchEvent(new Event('auth-expired'));

      if (location.pathname !== '/') {
        location.href = '/';
      }
    }

    return Promise.reject(error);
  }
);
