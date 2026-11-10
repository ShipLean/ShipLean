import { ApiRequestError } from '@shiplean/core/api/client';
import { afterEach, describe, expect, it } from 'vitest';
import type { Api } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { aProject } from '../../test-support/builders';
import { apiError, restoreFetch } from '../../test-support/fakeApi';
import { designInsights, retroApi } from '../../test-support/retro';
import type { MergeAction } from '../actions';
import { retroExecutor } from '../executor';
import { describeMerge, reconcileMerge, sendMerge } from './mergeSend';

afterEach(restoreFetch);

// The sender goes through @shiplean/core, which reads the installed fake at fetch; the api argument is unused.
const api = {} as Api;
const projectId = aProject().id;

const action = (kind: MergeAction['kind'], proposalId: string): MergeAction => ({
  area: 'retro',
  kind,
  itemId: null,
  key: null,
  projectId,
  proposalId,
  fromName: 'CI screenshots',
  intoName: 'Screenshot baselines need CI',
});
const path = (id: string) => `/api/retros/theme-merge-proposals/${id}`;

describe('retro merge executor', () => {
  it('posts merge and keep apart', async () => {
    const insights = designInsights();
    const id = insights.mergeProposals[0].id;
    const fake = retroApi({}, { projectId, insights });

    await expect(sendMerge(action('mergeThemes', id), api)).resolves.toBeUndefined();
    expect(fake.callsTo(`POST ${path(id)}/merge`)).toHaveLength(1);
    expect(fake.callsTo(`POST ${path(id)}/keep-apart`)).toHaveLength(0);

    await sendMerge(action('keepApart', id), api);
    expect(fake.callsTo(`POST ${path(id)}/keep-apart`)).toHaveLength(1);
    expect(fake.callsTo(`POST ${path(id)}/merge`)).toHaveLength(1);

    // A 409 (no longer pending) reaches the runner as the core's error.
    retroApi(
      { [`POST ${path('gone')}/merge`]: apiError(409, 'conflict', 'Already answered.') },
      { projectId },
    );
    const failure = await sendMerge(action('mergeThemes', 'gone'), api).catch((e: unknown) => e);
    expect(failure).toBeInstanceOf(ApiRequestError);
    expect((failure as ApiRequestError).status).toBe(409);

    // Neither can be taken back, so Undo reports them as staying.
    expect(retroExecutor.reversible('mergeThemes')).toBe(false);
    expect(retroExecutor.reversible('keepApart')).toBe(false);
  });

  it('describes merge and keep apart as the design does', () => {
    expect(describeMerge(action('mergeThemes', 'p1'))).toEqual({
      id: 'retro:mergeThemes:p1',
      icon: 'dot',
      text: 'Merge CI screenshots into Screenshot baselines need CI',
      removable: true,
    });
    expect(describeMerge(action('keepApart', 'p1'))).toMatchObject({
      id: 'retro:keepApart:p1',
      text: 'Keep CI screenshots and Screenshot baselines need CI apart',
    });
  });

  it('reconciles an uncertain merge as sent once the proposal is no longer pending, else notSent', async () => {
    const insights = designInsights();
    const still = insights.mergeProposals[0].id;
    const fake = retroApi({}, { projectId, insights });
    const items = [
      aPending({ id: 'a', action: action('mergeThemes', still) }),
      aPending({ id: 'b', action: action('keepApart', 'settled') }),
      aPending({ id: 'c', action: action('keepApart', still) }),
    ];
    await expect(reconcileMerge(items, api)).resolves.toEqual(['notSent', 'sent', 'notSent']);
    // One read of the action's own project, never one per pending and no project list.
    expect(fake.callsTo(`GET /api/projects/${projectId}/retros/insights`)).toHaveLength(1);
    expect(fake.callsTo('GET /api/projects')).toHaveLength(0);
  });
});
