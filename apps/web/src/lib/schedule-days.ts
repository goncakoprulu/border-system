export const scheduleDayLabels = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"] as const;

const namedDays: Record<string, number> = {
  Sunday: 0, Monday: 1, Tuesday: 2, Wednesday: 3, Thursday: 4, Friday: 5, Saturday: 6,
};
const loggedInvalidScheduleDays = new Set<string>();

export function normalizeScheduleDay(value: unknown): number | null {
  const numeric = typeof value === "number" ? value : typeof value === "string" && /^\d$/.test(value) ? Number(value) : typeof value === "string" ? namedDays[value] : undefined;
  if (numeric !== undefined && Number.isInteger(numeric) && numeric >= 0 && numeric <= 6) return numeric;
  const logKey = String(value);
  if (!loggedInvalidScheduleDays.has(logKey)) {
    loggedInvalidScheduleDays.add(logKey);
    console.error("Geçersiz ClassSchedule.dayOfWeek değeri alındı.", { value });
  }
  return null;
}

export function scheduleDayLabel(value: unknown) {
  const day = normalizeScheduleDay(value);
  return day === null ? "Bilinmeyen gün" : scheduleDayLabels[day];
}

export function scheduleDayTimeText(value: unknown, startTime: string, endTime: string) {
  return `${scheduleDayLabel(value)} ${startTime.slice(0, 5)}–${endTime.slice(0, 5)}`;
}
