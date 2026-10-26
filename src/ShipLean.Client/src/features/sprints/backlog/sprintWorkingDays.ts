const DAY_MS = 86_400_000;

const toUtc = (iso: string) => Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10));

const toIso = (ms: number) => new Date(ms).toISOString().slice(0, 10);

/** The yyyy-MM-dd dates from start to end (both included) whose ISO weekday (1 = Monday .. 7 = Sunday) is a working day. Used for position only, never for display. */
export function sprintWorkingDays(
  startDate: string,
  endDate: string,
  workingDays: readonly number[],
): string[] {
  const days: string[] = [];
  for (let ms = toUtc(startDate); ms <= toUtc(endDate); ms += DAY_MS) {
    const weekday = new Date(ms).getUTCDay() || 7;
    if (workingDays.includes(weekday)) days.push(toIso(ms));
  }
  return days;
}
