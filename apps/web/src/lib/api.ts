import { siteConfig } from "@/lib/site-config";

export type CurrentUser = {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
};

export class ApiError extends Error {
  constructor(public status: number, message: string, public errors?: Record<string, string[]>) {
    super(message);
  }
}

const requestTimeoutMs = 20_000;

function statusMessage(status: number, serverMessage?: string) {
  if (status === 401) return "Oturumunuz sona erdi. Lütfen tekrar giriş yapın.";
  if (status === 403) return "Bu işlemi yapmaya yetkiniz yok.";
  if (status === 404) return serverMessage || "İstenen kayıt bulunamadı.";
  if (status === 409) return serverMessage || "İşlem mevcut bir kayıtla çakışıyor. Lütfen bilgileri kontrol edin.";
  if (status >= 500) return "Sunucuda beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
  return serverMessage || "İşlem tamamlanamadı. Lütfen bilgileri kontrol edin.";
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const controller = new AbortController();
  const timeout = globalThis.setTimeout(() => controller.abort(), requestTimeoutMs);
  try {
    const response = await fetch(`${siteConfig.apiUrl}${path}`, {
      ...init,
      signal: controller.signal,
      credentials: "include",
      headers: { "Content-Type": "application/json", ...init?.headers },
    });
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      const serverMessage = typeof body?.detail === "string" ? body.detail : typeof body?.title === "string" ? body.title : undefined;
      throw new ApiError(response.status, statusMessage(response.status, serverMessage), body?.errors);
    }
    return response.status === 204 ? (undefined as T) : response.json();
  } catch (error) {
    if (error instanceof ApiError) throw error;
    if (error instanceof DOMException && error.name === "AbortError") throw new ApiError(0, "İstek zaman aşımına uğradı. Lütfen tekrar deneyin.");
    throw new ApiError(0, "Sunucuya ulaşılamadı. Lütfen tekrar deneyin.");
  } finally {
    globalThis.clearTimeout(timeout);
  }
}

export async function apiQuery<T>(path: string) {
  return request<T>(path);
}

export async function apiMutation<T>(path: string, method: "POST" | "PUT" | "PATCH" | "DELETE", body?: unknown) {
  const token = await csrfToken();
  return request<T>(path, {
    method,
    headers: { "X-XSRF-TOKEN": token },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

async function csrfToken() {
  const result = await request<{ token: string }>("/api/auth/csrf");
  return result.token;
}

export const authApi = {
  me: () => request<CurrentUser>("/api/auth/me"),
  login: async (input: { email: string; password: string; rememberMe: boolean }) => {
    const token = await csrfToken();
    return request<CurrentUser>("/api/auth/login", {
      method: "POST",
      headers: { "X-XSRF-TOKEN": token },
      body: JSON.stringify(input),
    });
  },
  logout: async () => {
    const token = await csrfToken();
    return request<void>("/api/auth/logout", { method: "POST", headers: { "X-XSRF-TOKEN": token } });
  },
};
