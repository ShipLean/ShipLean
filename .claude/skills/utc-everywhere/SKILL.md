---
name: utc-everywhere
description: The date and time rule for ShipLean. Everything from the BFF back is UTC, and time zones exist only at the last mile, where the BFF renders a display string. Use when writing or reviewing anything that touches a date, time or timestamp: a column or migration, SQL with now() or a day boundary, a C# DateTimeOffset/DateOnly, a DTO field or display label, an endpoint that accepts a date, a browser date input, or server, container or Postgres time settings.
---

# UTC everywhere

**Every date-time from the BFF back is UTC. No exceptions.** The database, SQL, services, events, logs, the servers, the containers and Postgres all run on UTC. No time zone is stored, carried, passed or computed with anywhere behind the BFF. UTC is the assumption, not a label: a value carries no zone because it never needs one.

A time zone appears in exactly **one** place: the **last mile**, where the BFF (`ShipLean.WebApi` and the Application services behind it) turns a UTC instant into a display string for the viewer. Its mirror is the **inbound edge**, where the BFF validates a date the browser sent and converts it to UTC. Those are the only two places where zone code belongs.

**Why this is non-negotiable:** once a zone gets into the backend, daylight saving makes values travel through time. An hour repeats, an hour vanishes, "midnight" doesn't exist on some days, and two machines disagree about what day it is. Every conversion is a bug waiting to happen. With one zone there are no conversions to get wrong.

## Where each layer stands

| Layer | The rule |
|---|---|
| Servers, containers, tests, Postgres | Run on UTC whatever the machine's own zone: `TZ=UTC` (the process's zone) and `PGTZ=UTC` (each Postgres session's `TimeZone`) are pinned in the Dockerfile, `docker-compose.yml`, `src/shiplean.runsettings` (every `dotnet test` process) and `launchSettings.json` (`dotnet run`); acceptance browser contexts use `TimezoneId = "UTC"` (a viewer-zone test inherits `BangkokViewerTest`, which runs the browser and the signed-in user in Asia/Bangkok). A new process, image or test entry point gets the same pins. Code still never relies on the host's zone: it asks `TimeProvider` for UTC. |
| Database columns | An instant is `timestamptz`. Despite the name it **stores no zone**: Postgres normalises the value to a UTC instant and keeps only that. A calendar day with no time of day (`due_on`) is `date`. Never add a zone-name or offset column, and never use `timestamp without time zone` to hold a wall-clock time. |
| SQL | Stamp with `now()`, and do arithmetic with `interval` (`now() + interval '14 days'`). A day boundary (`current_date`, `ts::date`, `date_trunc('day', ts)`) depends on the session's `TimeZone`: pass the day from C# as a parameter, or write `(ts AT TIME ZONE 'UTC')::date` so the zone is fixed. |
| C# | The clock is `TimeProvider.GetUtcNow()` (`BannedSymbols.txt` bans `DateTime.Now`/`UtcNow`). Instants are `DateTimeOffset` with offset zero. Pass UTC to Npgsql, which rejects any other offset; `DapperTypeHandlers` reads `timestamptz` back as offset zero. A calendar day is `DateOnly`. Zone code (`TimeZoneInfo` conversions and lookups) lives only in `DisplayDate`; a service takes the viewer's zone from `IViewerZone.Zone` and passes it in. Never read `clock.LocalTimeZone`, `TimeZoneInfo.Local` or call `ToLocalTime()`. |
| Events, logs | UTC instants. Labels are the last mile's job, so they are built there and nowhere else. |
| BFF → browser (last mile) | A DTO carries the UTC value as an ISO string (`lastEditedAt`) **plus** a server-formatted label (`lastEditedLabel`) from `ShipLean.Core.DisplayDate`, converted to the viewer's zone (`IViewerZone.Zone`). That conversion is the only outbound zone code in the system. |
| Browser | Renders the label. It treats ISO and `yyyy-MM-dd` strings as **opaque**: it compares and sorts them as strings (they sort correctly) and sends them back unchanged. It never makes a `Date`, imports a date library, calls `Intl.DateTimeFormat`/`toLocale*String` on a date, or does date arithmetic. If it needs "next week" or "is this overdue", the BFF computes it and sends the answer. |
| Browser → BFF (inbound edge) | Data entry is the one legitimate case of date handling in the browser: an `<input type="date">` value goes up as the `yyyy-MM-dd` string it already is. Then the BFF validates it hard (below). |

## The viewer's zone

**Where it comes from.** Each user has a zone: `app_user.time_zone`, an IANA id such as `Asia/Bangkok` (NULL = not set, labels in UTC). The migration backfilled every existing user with `Asia/Bangkok`; a new user starts NULL until the browser capture sets it. The SPA shell captures the browser's zone once on first sign-in (`src/features/shell/timeZone/browserZone.ts`, the only file allowed to call `Intl.DateTimeFormat`; it sends `PUT /api/me/time-zone` with source `browser`, which the server applies only while the column is NULL). The person changes it in the Profile page's Time zone panel (source `profile`, choices from `GET /api/me/time-zones`). The phone app has no control; its labels follow the web setting. Ids are checked by `DisplayDate.TryZone` (IANA ids only).

**How the server reads it.** Inject `IViewerZone` (`ShipLean.Contracts`, a singleton like `ICurrentUser`) and pass `viewerZone.Zone` with `time.GetUtcNow()` to a zone-taking `DisplayDate` function. Resolve it once per read and pass it down to static row mappers. Cookie, device and MCP bearers all carry the user, so MCP tool output is labelled in the agent's user's zone too (JSON keeps the ISO values). The zone reaches `IViewerZone` as a claim: `ViewerZoneClaims` (an `IClaimsTransformation`) adds it to the request's user from `UserSessions`. No claim (no request user, an unset zone or an unknown id) falls back to UTC.

**What stays UTC.** Text written for agents or the public, not a viewer: agent prompt context, plan briefs, demo upload deadlines, public GitHub issue posts and backup cron times. These use `DisplayDate.Format` ("2026-11-16 00:02 UTC") or ISO 8601 with `'T'` and `Z`, so the zone is always named. Sprint days ("Ends today", which sprint is current, the burndown's working days) are calendar days shared by the whole project, so `SprintLabels` and `SprintMetrics` keep the UTC day (SHIP-1702 D8); the server sends the working days and the client does no date arithmetic.

**The standard formats** (all in `DisplayDate`, 24-hour, day-first, English invariant, no locale setting):

| Function | Example | For |
|---|---|---|
| `Relative(value, now, zone)` | "just now", "12m ago", "2h ago", "Yesterday", "30 Sep", "2 Mar 2025" | "last edited" style labels |
| `ShortDay(value, now, zone)` | "30 Sep", "2 Mar 2025" | a day, with the year only when it is not this year |
| `DayLabel(value, zone)` | "Mon 5 Oct" | a day with its weekday |
| `DayTime(value, zone)` | "Mon 5 Oct, 07:02" | a day and time |
| `Time(value, zone)` | "07:02" | a time of day |
| `LogTime(value, zone)` | "07:02:05" | log and trace rows |
| `ShortWeekday(value, zone)` | "Mon" | "since Mon 09:12" |
| `DayAt(value, now, zone)` | "today at 07:02", "2 Mar at 07:02" | an event's moment |
| `ZoneLabel(zone, now)` | "Asia/Bangkok (UTC+07:00)" | the Profile panel |
| `WeekdayDay(day)` | "Thu 25 Dec" | a `DateOnly` calendar day (no zone) |
| `Duration`, `Gap`, `Age` | "3.5 h", "3.7 days", "14h" | zone-free spans and ages |
| `Format(value)` | "2026-11-16 00:02 UTC" | agent and machine text only |

Day arithmetic in the viewer's zone goes through `DisplayDate` too: `Today(now, zone)` (an entered day's bound), `DayOf(value, zone)` (an instant's day, e.g. a date input's value), `DaysUntil(value, now, zone)` ("due in 3 days") and `StartOfDay(day, zone)` (an entered day as a UTC instant). Don't hand-write `TimeZoneInfo.ConvertTime` in a service.

**Tests.** Expected labels are literal strings ("07:02", "Asia/Bangkok (UTC+07:00)") or worked out by hand from the instant (`value.ToOffset(TimeSpan.FromHours(7))`), never through `DisplayDate`, so a wrong `DisplayDate` cannot also make the expectation wrong. `ViewerZoneTestData.LateEveningUtc` (20:00 UTC, the next day in Bangkok) and `ViewerZoneArrange.SetAsync` set up a viewer; acceptance tests use `ExpectedLabels`. Each viewer-zone test names its mutation (pass `TimeZoneInfo.Utc` instead of `viewerZone.Zone`) and is shown red once.

**Guard rails.** `ViewerTimeZoneTests` (UnitTest, `Architecture`) scans the production projects with `ViewerTimeZoneScanner` and fails on zone code (`LocalTimeZone`, `TimeZoneInfo.Local`, `ToLocalTime`, `ConvertTime`, zone lookups) or an ad-hoc date format (`"d MMM"`, `"HH:mm"`, `"ddd"` that is neither ISO nor names UTC) outside `DisplayDate.cs`; the sprint day format (`"d MMM"`, calendar days, D8) is `DisplayDate.SprintDates`, so `DisplayDate.cs` is the only exempt file. There is no baseline: every hit fails. In the SPA, the phone app and `@shiplean/core`, the lint rule `shiplean/no-client-dates` (`eslint/dates.js`) bans `new Date`, `Intl.DateTimeFormat`/`RelativeTimeFormat`, `toLocaleDateString`/`TimeString`, Date getters and date libraries outside tests; `Date.now()`/`Date.parse()`/`Date.UTC()` stay allowed for timers and comparisons. Its only exceptions are named in `eslint.config.js`, and an allowed `new Date` covers only the first construction in the file: `browserZone.ts` (one `Intl` read), the phone's `src/actions/clock.ts` (its single `new Date`, behind `systemNow` and `isoAt`) and the phone's `inputLabels.ts` (the mic-tuning "tested today" label: a timestamp the phone stamps itself, never sent by the server, so one `new Date` and `getTimezoneOffset` bucket it into local days). A converting `TimeZoneInfo.ConvertTime(..., TimeZoneInfo.Utc)` is not flagged: converting to explicit UTC leaks no viewer zone.

## Accepting a date at the inbound edge

Anything that comes out of the browser is untrusted. Validate dates as harshly as passwords. For every date field an endpoint accepts:

1. **The request field is a `string`**, not `DateTime`/`DateTimeOffset`. Model binding accepts any culture's format and any offset, and that is exactly the looseness that lets a zone in.
2. **Parse with one exact format**: `DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)`. A time of day is a separate `HH:mm` string, parsed the same way. Never use `DateTime.Parse`, `Convert.ToDateTime`, a culture-sensitive overload, or a fallback list of formats.
3. **Bound it** to what the feature allows (not in the past, at most a year out, a sane year range) and reject anything else with `ApiException.Validation`, naming the expected format: "The reminder date must be written yyyy-MM-dd."
4. **Convert to UTC once, right here.** A pure calendar day stays a `DateOnly` and is stored as `date`. A day or wall-clock time that marks a moment ("remind me on 30 Oct") is in the viewer's zone: convert it to a UTC `DateTimeOffset` with that zone in this one line (`DisplayDate.StartOfDay` for a day). A wall-clock time can fall in a daylight-saving gap or overlap (`TimeZoneInfo.IsInvalidTime` / `IsAmbiguousTime`), so reject it or resolve it on purpose: `StartOfDay` takes the first instant of the day when midnight is skipped or repeated. After this line no zone exists.
5. **Test the edge**, in the feature's API tests: wrong format (`30/10/2026`, `2026-10-30T00:00`, a trailing offset), an impossible date (`2026-02-30`), an out-of-range date, and the happy path. Compute expected values from the run's clock (time is never frozen).

## Red flags

Each of these lets a zone in. Use the fix instead.

| Pattern | Fix |
|---|---|
| `new Date(...)`, Date getters, dayjs/date-fns/moment, `Intl.DateTimeFormat`, date arithmetic in the SPA or phone app | A server label, or a server-computed field (`Date.now()`/`Date.parse()` only for timers and comparisons) |
| A column named `*_tz`, `timezone`, `utc_offset`, or `timestamp without time zone` holding a local time | `timestamptz` (a UTC instant) or `date` (a calendar day) |
| `current_date`, `::date`, `date_trunc('day', …)` in SQL with no explicit zone | A day passed from C#, or `AT TIME ZONE 'UTC'` |
| `DateTimeOffset` with a non-zero offset, `DateTime` of kind `Local`/`Unspecified`, `ToLocalTime()` | `clock.GetUtcNow()`, `DateOnly` for days |
| `TimeZoneInfo` conversions or `LocalTimeZone` outside `DisplayDate` | A zone-taking `DisplayDate` function with `IViewerZone.Zone` |
| A label built with the server's zone, or an agent prompt time that names no zone | `IViewerZone.Zone` for a viewer; `DisplayDate.Format` (explicit UTC) for agent text |
| A DTO date-time field typed `DateTime`/`DateTimeOffset` on a **request** | `string`, parsed by the steps above |
| A label built in the browser or stored in the database | `DisplayDate` at the last mile |

**When reviewing**, account for every date in the diff: each new column, SQL expression, DTO field, `TimeZoneInfo` use and browser date reference sits in a layer of the table above and follows its rule. This lists the candidates:

```bash
git diff origin/main... | grep -nE '^\+.*(timestamp|::date|current_date|date_trunc|AT TIME ZONE|DateTime|DateOnly|TimeZoneInfo|LocalTimeZone|ToLocalTime|new Date|Date\.|Intl\.|type="(date|time|datetime-local)")'
```
