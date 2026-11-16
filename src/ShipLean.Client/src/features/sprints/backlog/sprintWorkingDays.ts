/** Days since 1970-01-01 for a proleptic Gregorian civil date (Hinnant's days-from-civil). */
function daysFromCivil(y: number, m: number, d: number): number {
  const year = m <= 2 ? y - 1 : y;
  const era = Math.floor(year / 400);
  const yoe = year - era * 400;
  const doy = Math.floor((153 * (m + (m > 2 ? -3 : 9)) + 2) / 5) + d - 1;
  const doe = yoe * 365 + Math.floor(yoe / 4) - Math.floor(yoe / 100) + doy;
  return era * 146097 + doe - 719468;
}

/** The civil date (year, month, day) for days since 1970-01-01. */
function civilFromDays(days: number): [number, number, number] {
  const z = days + 719468;
  const era = Math.floor(z / 146097);
  const doe = z - era * 146097;
  const yoe = Math.floor(
    (doe - Math.floor(doe / 1460) + Math.floor(doe / 36524) - Math.floor(doe / 146096)) / 365,
  );
  const doy = doe - (365 * yoe + Math.floor(yoe / 4) - Math.floor(yoe / 100));
  const mp = Math.floor((5 * doy + 2) / 153);
  const d = doy - Math.floor((153 * mp + 2) / 5) + 1;
  const m = mp < 10 ? mp + 3 : mp - 9;
  return [yoe + era * 400 + (m <= 2 ? 1 : 0), m, d];
}

const toDays = (iso: string) =>
  daysFromCivil(+iso.slice(0, 4), +iso.slice(5, 7), +iso.slice(8, 10));

const pad = (n: number, width: number) => String(n).padStart(width, '0');

const toIso = (days: number) => {
  const [y, m, d] = civilFromDays(days);
  return `${pad(y, 4)}-${pad(m, 2)}-${pad(d, 2)}`;
};

/** ISO weekday (1 = Monday .. 7 = Sunday); day 0 (1970-01-01) was a Thursday. */
const isoWeekday = (days: number) => ((((days + 3) % 7) + 7) % 7) + 1;

/** The yyyy-MM-dd dates from start to end (both included) whose ISO weekday (1 = Monday .. 7 = Sunday) is a working day. Used for position only, never for display. */
export function sprintWorkingDays(
  startDate: string,
  endDate: string,
  workingDays: readonly number[],
): string[] {
  const days: string[] = [];
  for (let day = toDays(startDate); day <= toDays(endDate); day += 1) {
    if (workingDays.includes(isoWeekday(day))) days.push(toIso(day));
  }
  return days;
}
