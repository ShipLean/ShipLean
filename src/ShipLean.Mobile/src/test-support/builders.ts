// Fake data, starting from the pinned wire shapes (src/ShipLean.TestSupport/DtoFixtures) where there is one.
import type { DeviceSessionDto, DeviceSignInDto } from '@shiplean/core/api/deviceSessions';
import type { HealthDto } from '@shiplean/core/api/health';
import type { MeDto } from '@shiplean/core/api/me';
import type { MemberDto, ProjectSummaryDto, StateDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import type { ConflictDto, WorkItemDto, WriteResult } from '@shiplean/core/api/workItems';
import deviceSignIn from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/device-sign-in.json';
import members from '../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import states from '../../../ShipLean.TestSupport/DtoFixtures/projects/states.json';
import sprints from '../../../ShipLean.TestSupport/DtoFixtures/sprints/sprints.json';
import conflictHistory from '../../../ShipLean.TestSupport/DtoFixtures/workItems/conflict-history.json';
import featureItem from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.feature.json';
import storyItem from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.story.json';

// The server every signed-in test talks to.
export const TEST_ORIGIN = 'https://shiplean.test';
export const TEST_SECRET = 'shl_dev_TestSecret0000000000000000000000000000000';

const fixture = deviceSignIn as DeviceSignInDto;

export function aMe(overrides: Partial<MeDto> = {}): MeDto {
  return { ...fixture.me, ...overrides };
}

export function aProject(overrides: Partial<ProjectSummaryDto> = {}): ProjectSummaryDto {
  return {
    id: '0199f000-0000-7000-8000-0000000000a1',
    key: 'SHIP',
    name: 'ShipLean',
    role: 'member',
    memberCount: 3,
    ...overrides,
  };
}

export function aHealth(overrides: Partial<HealthDto> = {}): HealthDto {
  return { status: 'ok', version: '1.4.0+7f26ef0a', search: null, ...overrides };
}

export function aDeviceSession(overrides: Partial<DeviceSessionDto> = {}): DeviceSessionDto {
  return { ...fixture.session, ...overrides };
}

export function aDeviceSignIn(overrides: Partial<DeviceSignInDto> = {}): DeviceSignInDto {
  return { ...fixture, secret: TEST_SECRET, ...overrides };
}

// A Feature work item (work-item.feature.json).
export function aWorkItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return { ...(featureItem as WorkItemDto), ...overrides };
}

// A User Story work item (work-item.story.json).
export function aStory(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return { ...(storyItem as WorkItemDto), ...overrides };
}

// A write that changed nothing else: the story at its next revision. There is no pinned fixture for it.
export function aWriteResult(overrides: Partial<WriteResult> = {}): WriteResult {
  const story = aStory();
  return {
    id: story.id,
    key: story.key,
    revision: story.revision + 1,
    changes: [],
    notices: [],
    ...overrides,
  };
}

// The 409 body for a stale write, with the item as it is now (the conflict in conflict-history.json).
export function aConflict(current: WorkItemDto, overrides: Partial<ConflictDto> = {}): ConflictDto {
  return { ...(conflictHistory.conflict as ConflictDto), current, ...overrides };
}

// The first state in projects/states.json.
export function aState(overrides: Partial<StateDto> = {}): StateDto {
  return { ...(states[0] as StateDto), ...overrides };
}

// The first member in projects/members.json.
export function aMember(overrides: Partial<MemberDto> = {}): MemberDto {
  return { ...(members[0] as MemberDto), ...overrides };
}

// The current sprint in sprints/sprints.json.
export function aSprint(overrides: Partial<SprintDto> = {}): SprintDto {
  const current = sprints.find((s) => s.status === 'current');
  if (current === undefined) throw new Error('sprints.json has no current sprint.');
  return { ...(current as SprintDto), ...overrides };
}
