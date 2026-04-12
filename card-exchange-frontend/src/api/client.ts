import axios, { type InternalAxiosRequestConfig } from 'axios';

const API_BASE = '/api';

const client = axios.create({
  baseURL: API_BASE,
  headers: { 'Content-Type': 'application/json' },
});

client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem('token');
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let isRefreshing = false;
let refreshSubscribers: Array<(token: string) => void> = [];

function onRefreshed(token: string) {
  refreshSubscribers.forEach((cb) => cb(token));
  refreshSubscribers = [];
}

function addRefreshSubscriber(cb: (token: string) => void) {
  refreshSubscribers.push(cb);
}

function forceLogout() {
  localStorage.removeItem('token');
  localStorage.removeItem('refreshToken');
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

client.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config;

    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;

      if (isRefreshing) {
        return new Promise((resolve) => {
          addRefreshSubscriber((token: string) => {
            original.headers.Authorization = `Bearer ${token}`;
            resolve(client(original));
          });
        });
      }

      isRefreshing = true;
      const refreshToken = localStorage.getItem('refreshToken');
      const accessToken = localStorage.getItem('token');

      if (refreshToken && accessToken) {
        try {
          const { data } = await axios.post(`${API_BASE}/auth/refresh-token`, {
            accessToken,
            refreshToken,
          });
          localStorage.setItem('token', data.accessToken);
          localStorage.setItem('refreshToken', data.refreshToken);
          isRefreshing = false;
          onRefreshed(data.accessToken);
          original.headers.Authorization = `Bearer ${data.accessToken}`;
          return client(original);
        } catch {
          isRefreshing = false;
          refreshSubscribers = [];
          forceLogout();
        }
      } else {
        isRefreshing = false;
        forceLogout();
      }
    }

    if (error.response?.status === 429) {
      const retryAfter = error.response.headers['retry-after'];
      if (retryAfter && !original._rateLimitRetry) {
        original._rateLimitRetry = true;
        const delay = parseInt(retryAfter, 10) * 1000 || 5000;
        await new Promise((resolve) => setTimeout(resolve, delay));
        return client(original);
      }
    }

    return Promise.reject(error);
  }
);

export default client;
