const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isGuid(value: string | null): value is string {
  return value !== null && guidPattern.test(value);
}

export function studentDetailHref(id: string) {
  return `/students/detail/?id=${encodeURIComponent(id)}`;
}

export function classDetailHref(id: string) {
  return `/classes/detail/?id=${encodeURIComponent(id)}`;
}
