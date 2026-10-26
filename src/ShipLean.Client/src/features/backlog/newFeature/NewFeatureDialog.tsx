import { useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiRequestError } from '../../../api/client';
import type { ProjectDto } from '../../../api/projects';
import { Dialog } from '../../../components/Dialog';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { useProjectStates } from '../../workItem/workItemApi';
import { backlogTestIds as ids } from '../testIds';
import { EpicSelect } from './EpicSelect';
import { useCreateFeature, useSlugCheck } from './newFeatureApi';
import { SlugField } from './SlugField';
import { slugFromTitle } from './slugFromTitle';
import { StartAs, startStatesOf } from './StartAs';

const Row = ({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor: string;
  children: ReactNode;
}) => (
  <div className="grid grid-cols-[96px_1fr] items-start gap-x-3 gap-y-1">
    <label htmlFor={htmlFor} className="pt-1.5 text-[length:var(--text-sm)] text-ink">
      {label}
    </label>
    <div className="grid gap-1">{children}</div>
  </div>
);

// The New Feature dialog: Create makes the Feature and opens it. The slug follows the title until it is edited by hand.
export function NewFeatureDialog({
  project,
  onClose,
}: {
  project: ProjectDto;
  onClose: () => void;
}) {
  const navigate = useNavigate();
  const create = useCreateFeature(project.id, project.key);
  const states = useProjectStates(project.id);
  const startOptions = startStatesOf(states.data ?? []);
  const [title, setTitle] = useState('');
  const [parentId, setParentId] = useState('');
  const [typedSlug, setTypedSlug] = useState<string | null>(null);
  const [goal, setGoal] = useState('');
  const [startId, setStartId] = useState<string | null>(null);
  const [statusLine, setStatusLine] = useState('');
  // A slug_taken from Create belongs to the slug it was refused for.
  const [refused, setRefused] = useState<{ slug: string; message: string } | null>(null);

  const slug = typedSlug ?? slugFromTitle(title);
  const check = useSlugCheck(project.id, slug);
  const answered = check.data?.value === slug ? check.data : undefined;
  const serverError = refused?.slug === slug ? refused.message : null;
  const slugOk = slug === '' || (answered?.available === true && serverError === null);
  const start = startId ?? startOptions[0]?.id ?? '';

  const canCreate = title.trim() !== '' && slugOk && !create.isPending;

  function submit() {
    if (!canCreate) return;
    create.mutate(
      {
        title: title.trim(),
        parentId: parentId === '' ? null : parentId,
        slug: slug === '' ? null : slug,
        goal: goal.trim(),
        statusLine: statusLine.trim(),
        // The first start state is what a create without a state does anyway.
        stateId: start !== '' && start !== startOptions[0]?.id ? start : null,
      },
      {
        onSuccess: (written) => {
          onClose();
          void navigate(routePath('workItem', { key: project.key, itemKey: written.key }));
        },
        onError: (error) => {
          if (error instanceof ApiRequestError && error.code === 'slug_taken') {
            setRefused({ slug, message: error.message });
          }
        },
      },
    );
  }

  const failure =
    create.error && !(create.error instanceof ApiRequestError && create.error.code === 'slug_taken')
      ? create.error instanceof ApiRequestError
        ? create.error.message
        : 'Could not create the Feature.'
      : null;

  return (
    <Dialog
      title="New Feature"
      onClose={onClose}
      width="560px"
      footer={
        <>
          <p className="m-0 flex-1 text-[length:var(--text-xs)] text-ink-muted">
            Description, links and attachments come after it&apos;s created. Agents use{' '}
            <code>create_feature</code> with the same fields.
          </p>
          {failure ? (
            <span data-testid={ids.newFeatureError} role="alert" className="text-alert">
              {failure}
            </span>
          ) : null}
          <button
            type="button"
            data-testid={ids.newFeatureCancel}
            className="btn-ghost"
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.newFeatureCreate}
            className="btn-signal disabled:opacity-50"
            disabled={!canCreate}
            onClick={submit}
          >
            Create Feature
          </button>
        </>
      }
    >
      <form
        className="grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
      >
        <Row label="Title" htmlFor="new-feature-title">
          <div className="flex items-center gap-2">
            <TypeIcon type="feature" />
            <input
              id="new-feature-title"
              data-testid={ids.newFeatureTitle}
              autoFocus
              className="field flex-1"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </div>
        </Row>
        <Row label="Epic" htmlFor="new-feature-epic">
          <EpicSelect projectId={project.id} value={parentId} onChange={setParentId} />
        </Row>
        <Row label="Slug" htmlFor="new-feature-slug">
          <SlugField
            value={slug}
            onChange={setTypedSlug}
            check={answered}
            serverError={serverError}
          />
        </Row>
        <Row label="Goal" htmlFor="new-feature-goal">
          <textarea
            id="new-feature-goal"
            data-testid={ids.newFeatureGoal}
            rows={3}
            className="field h-auto w-full py-1.5"
            value={goal}
            onChange={(event) => setGoal(event.target.value)}
          />
          <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
            The outcome, not the activity. One or two sentences.
          </p>
        </Row>
        {startOptions.length > 0 ? (
          <Row label="Start as" htmlFor="new-feature-start">
            <StartAs options={startOptions} value={start} onChange={setStartId} />
          </Row>
        ) : null}
        <Row label="Status" htmlFor="new-feature-status">
          <input
            id="new-feature-status"
            data-testid={ids.newFeatureStatus}
            className="field w-full"
            value={statusLine}
            onChange={(event) => setStatusLine(event.target.value)}
          />
        </Row>
      </form>
    </Dialog>
  );
}
