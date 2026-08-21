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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${siteConfig.apiUrl}${path}`, {
    ...init,
    credentials: "include",
    headers: { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new ApiError(response.status, body?.detail ?? body?.title ?? "İşlem tamamlanamadı.", body?.errors);
  }
  return response.status === 204 ? (undefined as T) : response.json();
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
