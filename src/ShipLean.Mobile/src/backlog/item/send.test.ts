import { afterEach, describe, expect, it } from 'vitest';
import type { Api } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { NOTE_ID, backlogApi } from '../../test-support/backlog';
import { aMe } from '../../test-support/builders';
import { controlledApi } from '../../test-support/controlledApi';
import { restoreFetch } from '../../test-support/fakeApi';
import { aComment, aDecision, anItemAsks } from '../../test-support/workItem';
import type { BacklogAddNote, BacklogApprove } from '../actions';
import {
  describeAddNote,
  describeApprove,
  reconcileItem,
  sendAddNote,
  sendApprove,
  undoAddNote,
} from './send';

afterEach(restoreFetch);

// The runner's api argument is unused: the senders go through @shiplean/core, which reads the installed fake.
const api = {} as Api;
const ITEM = 'item-906';
const approve: BacklogApprove = {
  area: 'backlog',
  kind: 'approve',
  itemId: ITEM,
  key: 'SHIP-906',
  numbers: [10, 11],
};
const note: BacklogAddNote = {
  area: 'backlog',
  kind: 'addNote',
  itemId: ITEM,
  key: 'SHIP-906',
  body: '  Ask Tom about the 4 GB phones.  ',
};

describe('backlog item executor', () => {
  it('sends approve through the asks write', async () => {
    const fake = controlledApi(
      backlogApi({ [`POST /api/work-items/${ITEM}/decisions/confirm`]: { status: 204 } }),
    );
    await sendApprove(approve, api);
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/decisions/confirm`)[0].body).toEqual({
      numbers: [10, 11],
    });
    expect(describeApprove(approve)).toMatchObject({
      text: 'Approve D10, D11 on SHIP-906',
      detail: 'Posted on SHIP-906. The next agent to pick it up reads it.',
    });
  });

  it('posts a note with exactly its body', async () => {
    const fake = controlledApi(
      backlogApi({ [`POST /api/work-items/${ITEM}/comments`]: aComment(ITEM, { id: 'c-1' }) }),
    );
    const receipt = await sendAddNote(note, api);
    expect(fake.callsTo(`POST /api/work-items/${ITEM}/comments`)[0].body).toEqual({
      body: '  Ask Tom about the 4 GB phones.  ',
    });
    expect(receipt).toEqual({ createdId: 'c-1' });
    expect(describeAddNote(note).text).toBe(
      'Note on SHIP-906: "  Ask Tom about the 4 GB phones.  "',
    );
  });

  it('undoes a note by archiving it', async () => {
    const fake = controlledApi(backlogApi());
    await undoAddNote(note, api, { createdId: NOTE_ID });
    expect(fake.callsTo(`POST /api/comments/${NOTE_ID}/archive`)).toHaveLength(1);
  });

  it('fails the undo when the receipt is null', async () => {
    const fake = controlledApi(backlogApi());
    await expect(undoAddNote(note, api, null)).rejects.toThrow();
    await expect(undoAddNote(note, api)).rejects.toThrow();
    expect(fake.calls).toHaveLength(0);
  });

  it('reconciles an uncertain note and approve against the item', async () => {
    const me = aMe();
    const mine = (body: string, createdAt: string) =>
      aComment(ITEM, { body, createdAt, author: { ...aComment(ITEM).author!, id: me.id } });
    const theirs = aComment(ITEM, {
      body: note.body,
      createdAt: '2030-01-01T09:01:00.000Z',
      author: { ...aComment(ITEM).author!, id: 'someone-else' },
    });
    controlledApi(
      backlogApi({
        'GET /api/me': me,
        [`GET /api/work-items/${ITEM}/comments`]: [
          theirs,
          mine('An older note', '2030-01-01T09:01:00.000Z'),
          mine(note.body, '2030-01-01T09:00:30.000Z'),
        ],
        [`GET /api/work-items/${ITEM}/asks`]: anItemAsks(ITEM, {
          decisions: [
            aDecision({ number: 10, state: 'confirmed' }),
            aDecision({ number: 11, state: 'open' }),
          ],
        }),
      }),
    );
    const at = (a: BacklogAddNote | BacklogApprove) =>
      aPending({ action: a, createdAt: '2030-01-01T09:00:00.000Z' });
    expect(
      await reconcileItem(
        [
          at(note),
          at({ ...note, body: 'Never arrived' }),
          at({ ...approve, numbers: [10] }),
          at(approve),
        ],
        api,
      ),
    ).toEqual(['sent', 'notSent', 'sent', 'notSent']);
  });

  it("does not count another person's note or one older than the send", async () => {
    const me = aMe();
    controlledApi(
      backlogApi({
        'GET /api/me': me,
        [`GET /api/work-items/${ITEM}/comments`]: [
          aComment(ITEM, {
            body: note.body,
            createdAt: '2030-01-01T09:01:00.000Z',
            author: { ...aComment(ITEM).author!, id: 'someone-else' },
          }),
          aComment(ITEM, {
            body: note.body,
            createdAt: '2029-12-31T09:00:00.000Z',
            author: { ...aComment(ITEM).author!, id: me.id },
          }),
        ],
      }),
    );
    expect(
      await reconcileItem([aPending({ action: note, createdAt: '2030-01-01T09:00:00.000Z' })], api),
    ).toEqual(['notSent']);
  });
});
