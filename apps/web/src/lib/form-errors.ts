import { ApiError } from "@/lib/api";

export type FieldErrorSetter<T extends string> = (name: T, message: string) => void;

export function applyApiFieldErrors<T extends string>(error: unknown, setFieldError: FieldErrorSetter<T>, knownFields: readonly T[]) {
  if (!(error instanceof ApiError) || !error.errors) return false;
  let applied = false;
  for (const [serverKey, messages] of Object.entries(error.errors)) {
    const key = `${serverKey.charAt(0).toLowerCase()}${serverKey.slice(1)}` as T;
    if (!knownFields.includes(key) || !messages[0]) continue;
    setFieldError(key, messages[0]);
    applied = true;
  }
  return applied;
}

export function formErrorMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError) {
    if (error.status >= 500) return fallback;
    return error.message;
  }
  return fallback;
}
