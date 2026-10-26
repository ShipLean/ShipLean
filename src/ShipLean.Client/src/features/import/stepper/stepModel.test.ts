import { describe, expect, test } from 'vitest';
import { jiraProjects, mappingDto, mappingWithNewStatus, project } from '../ct/fixtures';
import {
  backStep,
  describeSteps,
  mappingSkipped,
  mappingValid,
  nextLabel,
  newStateCount,
  newUserCount,
  nextStep,
  targetOf,
} from './stepModel';

const rerunProject = { ...project, key: 'MOB', nextItemNumber: 9 };
const mobile = jiraProjects[1];
const ready = targetOf(project, jiraProjects[0]);
const rerun = targetOf(rerunProject, mobile);

const inputs = (mapping = mappingDto) => ({
  baseUrl: 'https://acme.atlassian.net',
  tested: true,
  jira: jiraProjects[0],
  target: ready,
  projectKey: 'SHIP',
  mapping,
  edited: false,
});

describe('mappingValid', () => {
  test('mappingValid_WhenEveryTypeIsSkipped_ShouldBeFalse', () => {
    const skipped = {
      ...mappingDto,
      issueTypes: mappingDto.issueTypes.map((t) => ({ ...t, target: 'skip' as const })),
    };
    expect(mappingValid(skipped)).toBe(false);
  });

  test('mappingValid_WhenAnImportedStatusHasNoState_ShouldBeFalse', () => {
    const blank = {
      ...mappingDto,
      statuses: mappingDto.statuses.map((s) => ({ ...s, targetState: ' ' })),
    };
    expect(mappingValid(blank)).toBe(false);
  });

  test('mappingValid_WhenOnlyASkippedTypeHasNoState_ShouldBeTrue', () => {
    const blank = {
      ...mappingDto,
      statuses: mappingDto.statuses.map((s) =>
        s.jiraType === 'Spike' ? { ...s, targetState: '' } : s,
      ),
    };
    expect(mappingValid(blank)).toBe(true);
  });

  test('mappingValid_WhenAMatchedPersonHasNoUser_ShouldBeFalse', () => {
    const orphan = { ...mappingDto, users: mappingDto.users.map((u) => ({ ...u, user: null })) };
    expect(mappingValid(orphan)).toBe(false);
  });
});

describe('describeSteps', () => {
  test('describeSteps_WhenMappingIsValid_ShouldReachRun', () => {
    expect(describeSteps(inputs()).reachable).toBe(5);
  });

  test('describeSteps_WhenMappingIsNotRead_ShouldStopAtMapping', () => {
    const state = describeSteps({ ...inputs(), mapping: null });
    expect(state.reachable).toBe(4);
    expect(state.canNext[3]).toBe(false);
  });

  test('describeSteps_WhenRerunHasNothingNew_ShouldSummariseMappingAsSameAsLastRun', () => {
    const state = describeSteps({ ...inputs(), jira: mobile, target: rerun, mapping: mappingDto });
    expect(state.skipped).toBe(true);
    expect(state.summaries[3]).toBe('Same as last run');
  });
});

describe('skipping the mapping', () => {
  test('mappingSkipped_WhenFirstImportHasNoNewRows_ShouldBeFalse', () => {
    expect(mappingSkipped(ready, mappingDto)).toBe(false);
  });

  test('mappingSkipped_WhenRerunHasANewStatus_ShouldBeFalse', () => {
    expect(mappingSkipped(rerun, mappingWithNewStatus)).toBe(false);
  });

  test('mappingSkipped_WhenRerunHasANewUser_ShouldBeFalse', () => {
    const newUser = {
      ...mappingDto,
      users: mappingDto.users.map((u, i) => (i === 0 ? { ...u, isNewSinceLastRun: true } : u)),
    };
    expect(mappingSkipped(rerun, newUser)).toBe(false);
  });

  test('mappingSkipped_WhenRerunHasANewIssueType_ShouldBeFalse', () => {
    const newType = {
      ...mappingDto,
      issueTypes: mappingDto.issueTypes.map((t, i) =>
        i === 0 ? { ...t, isNewSinceLastRun: true } : t,
      ),
    };
    expect(mappingSkipped(rerun, newType)).toBe(false);
  });

  test('describeSteps_WhenTheSkippedMappingWasEdited_ShouldNotBeSkipped', () => {
    const state = describeSteps({
      ...inputs(),
      jira: mobile,
      target: rerun,
      mapping: mappingDto,
      edited: true,
    });
    expect(state.skipped).toBe(false);
    expect(state.summaries[3]).toBe('1,200 issues');
  });

  test('nextStep_WhenSkipped_ShouldGoFromTargetToRunAndBack', () => {
    expect(nextStep(3, true)).toBe(5);
    expect(backStep(5, true)).toBe(3);
    expect(nextStep(3, false)).toBe(4);
    expect(backStep(5, false)).toBe(4);
    expect(nextLabel(3, true)).toBe('Run');
    expect(nextLabel(5, true)).toBe('');
  });
});

describe('what a run creates', () => {
  test('newUserCount_WhenOnePersonIsToBeCreated_ShouldCountThem', () => {
    expect(newUserCount(mappingDto)).toBe(1);
  });

  test('newStateCount_WhenOnlyAnImportedTypeCreatesAState_ShouldCountItOnce', () => {
    expect(newStateCount(mappingDto)).toBe(1);
  });
});
