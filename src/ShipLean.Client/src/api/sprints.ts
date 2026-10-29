// Sprints, capacity and metrics. C#: ShipLean.Contracts.Sprints. Nothing is omitted: absent values are null; dates are
// yyyy-MM-dd strings and labels are formatted by the server.
import type { SprintRef, SprintStatus, StateRef, UserRef, WorkItemTypeId } from './refs';

export const ROLLOVER_STATUSES = ['pending', 'decided'] as const;
export type RolloverStatus = (typeof ROLLOVER_STATUSES)[number];

export interface SprintDto {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  goal: string;
  status: SprintStatus;
  rolloverStatus: RolloverStatus | null;
  /** "20 Oct – 2 Nov". */
  datesLabel: string;
  /** "9 days left", "Ended 3 days ago", "Starts in 5 days". */
  timeLabel: string;
}

export interface SprintCreate {
  name: string;
  startDate: string;
  endDate: string;
  goal: string;
}

/** Null leaves a field as it is. */
export interface SprintUpdate {
  name: string | null;
  startDate: string | null;
  endDate: string | null;
  goal: string | null;
}

/** A national public holiday on a calendar date (C#: ShipLean.Contracts.Holidays.PublicHolidayDto). */
export interface PublicHolidayDto {
  date: string;
  name: string;
}

export interface MemberCapacityDto {
  user: UserRef;
  /** What was typed; null means the sprint's `defaultDays`. */
  days: number | null;
  /** Stored capacity: velocity / members * days / workingDays, rounded to 2 decimals (half away from zero), at most 9999.99. */
  points: number;
  note: string | null;
}

export interface SprintCapacityDto {
  sprintId: string;
  /** Only the entered rows: a member with no entry is absent and the sprint falls back to velocity. */
  members: MemberCapacityDto[];
  /** The sum of the members' points. */
  teamTotal: number;
  /** The stored velocity; null on an old points-only sprint. */
  velocity: number | null;
  /** What a new entry starts from: the latest earlier sprint's velocity, else the override, else the average, to 1 decimal. */
  velocityDefault: number;
  /** W: the sprint's dates on the project's working weekdays, holidays not removed. */
  workingDays: number;
  /** W minus the holidays on working days. */
  defaultDays: number;
  /** The country's holidays inside the sprint that fall on working weekdays. */
  holidays: PublicHolidayDto[];
  holidayCountry: string | null;
  /** An ended sprint's capacity is read-only. */
  ended: boolean;
}

export interface MemberCapacityPut {
  userId: string;
  /** 0..workingDays in steps of 0.5; null means the sprint's default. */
  days: number | null;
  note: string | null;
}

export interface SprintCapacityPut {
  /** 0..9999.99, two decimals. Required when `members` is not empty. */
  velocity: number | null;
  members: MemberCapacityPut[];
}

/** The velocity chip's override; null clears it (back to the average). */
export interface VelocityOverridePut {
  velocityOverride: number | null;
}

export interface BurndownPoint {
  date: string;
  remaining: number;
  ideal: number;
}

export interface SprintMetricsDto {
  sprintId: string;
  committed: number;
  completed: number;
  remaining: number;
  burndown: BurndownPoint[];
}

export interface VelocityEntryDto {
  sprintId: string;
  sprintName: string;
  committed: number;
  completed: number;
}

export interface VelocityDto {
  window: number;
  sprints: VelocityEntryDto[];
  average: number;
  override: number | null;
}

export interface ForecastSprintDto {
  /** Null for a hypothetical sprint past the last real one (named "Sprint 27"). */
  sprintId: string | null;
  name: string;
  /** What was free before the forecast filled it: capacity (when entered) or velocity, minus the points already in it. */
  room: number;
  fromCapacity: boolean;
  /** The cut line: the last backlog item forecast into the sprint, or null. */
  lastItemId: string | null;
  itemCount: number;
  /** Unestimated items count zero. */
  points: number;
  unestimatedCount: number;
}

export interface ForecastDto {
  sprints: ForecastSprintDto[];
}

export interface RolloverItemDto {
  workItemId: string;
  key: string;
  title: string;
  type: WorkItemTypeId;
  state: StateRef;
  assignee: UserRef | null;
  points: number | null;
  openTasks: number;
  totalTasks: number;
  defaultTargetSprintId: string | null;
}

export interface RolloverDto {
  sprint: SprintRef;
  status: RolloverStatus;
  items: RolloverItemDto[];
  nextSprint: SprintRef | null;
  nextSprintCapacity: number | null;
  /** Points already planned into the next sprint. */
  nextSprintPoints: number;
}

export interface RolloverMove {
  workItemId: string;
  /** Null = back to the backlog. */
  targetSprintId: string | null;
}

export interface RolloverRequest {
  moves: RolloverMove[];
}
