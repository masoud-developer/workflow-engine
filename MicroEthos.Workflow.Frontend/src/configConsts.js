let backendHost;
const apiVersion = 'v1';

const hostname = window && window.location && window.location.hostname;

if (hostname === 'plynx.com') {
  backendHost = 'https://plynx.com';
} else if (hostname === 'localhost') {
  backendHost = process.env.REACT_APP_BACKEND_HOST || 'http://localhost:6010';
} else {
  backendHost = '';
}

export const API_ENDPOINT = `${backendHost}/api/${apiVersion}/workflow`;
