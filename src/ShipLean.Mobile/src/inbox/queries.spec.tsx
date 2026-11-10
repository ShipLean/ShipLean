import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { Pressable, Text } from 'react-native';
import { aProject } from '../test-support/builders';
import { inboxApi } from '../test-support/inbox';
import { renderScreen } from '../test-support/renderScreen';
import { useAnsweredQuery, useInboxQuery, useInvalidateInbox } from './queries';

const project = aProject();
const INBOX = `GET /api/projects/${project.id}/inbox`;
const ANSWERED = `GET /api/projects/${project.id}/inbox/answered`;

function Probe({ projectId }: { projectId: string | undefined }) {
  const inbox = useInboxQuery(projectId);
  const answered = useAnsweredQuery(projectId);
  const invalidate = useInvalidateInbox();
  return (
    <>
      <Text testID="state">{`${inbox.status}/${answered.status}`}</Text>
      <Pressable testID="refresh" onPress={() => projectId !== undefined && invalidate(projectId)} />
    </>
  );
}

describe('the Inbox queries', () => {
  it('useInvalidateInbox refreshes both the Inbox and Answered', async () => {
    const api = inboxApi({}, { projectId: project.id });
    api.install();
    await renderScreen(<Probe projectId={project.id} />);
    await waitFor(() => expect(screen.getByTestId('state').props.children).toBe('success/success'));
    expect(api.callsTo(INBOX)).toHaveLength(1);
    expect(api.callsTo(ANSWERED)).toHaveLength(1);

    await fireEvent.press(screen.getByTestId('refresh'));

    await waitFor(() => expect(api.callsTo(INBOX)).toHaveLength(2));
    await waitFor(() => expect(api.callsTo(ANSWERED)).toHaveLength(2));
  });

  it("doesn't fetch without a project id", async () => {
    const api = inboxApi({}, { projectId: project.id });
    api.install();
    await renderScreen(<Probe projectId={undefined} />);
    await fireEvent.press(screen.getByTestId('refresh'));
    expect(screen.getByTestId('state').props.children).toBe('pending/pending');
    expect(api.callsTo(INBOX)).toHaveLength(0);
    expect(api.callsTo(ANSWERED)).toHaveLength(0);
    expect(api.calls.filter((c) => c.url.includes('/inbox'))).toHaveLength(0);
  });
});
