// The design's Inbox (design/mobileapp/inbox.html?state=list): 15 asks on 6 items, longest waiting first, SHIP-924's
// row under SHIP-907's. 10 decisions (8 on SHIP-907, 2 on SHIP-906), 3 questions (SHIP-892, SHIP-924, SHIP-906), a
// mention (SHIP-742) and a status line to check (SHIP-743). Pure; random ids.
import type { InboxAskDto, InboxDto } from '@shiplean/core/api/inbox';
import { aProject } from './builders';
import {
  aDecisionAsk,
  aMentionAsk,
  anInbox,
  anInboxItem,
  aQuestionAsk,
  aStatusAsk,
  aUser,
} from './inbox';

const SHIP_907_DECISIONS: [string, string, string][] = [
  [
    'Sign in through a browser handoff?',
    'Yes, the browser hands back a device secret',
    'A password form in the app',
  ],
  [
    'Share one core package between the web and the app?',
    'Yes, @shiplean/core',
    'Copy the client into the app',
  ],
  [
    'Give each phone its own device session?',
    'Yes, revocable from the web',
    'Share the web session',
  ],
  [
    'Keep the device secret in the Keychain or Keystore?',
    'Yes, secure storage only',
    'The app’s key-value store',
  ],
  ['Build with Expo?', 'Yes, Expo with a development build', 'Bare React Native'],
  ['Route with expo-router?', 'Yes, file-based routes', 'React Navigation by hand'],
  ['Cache reads with react-query?', 'Yes, as the web does', 'A store of our own'],
  ['Release through EAS?', 'Yes, EAS Build and Submit', 'Local builds'],
];

export function designInbox(projectId: string = aProject().id): InboxDto {
  const sam = aUser('Sam Lee', 'SL', '#3f9b6e');
  const priya = aUser('Priya Shah', 'PS', '#2f7fd8');
  const joel = aUser('Joel Dickson', 'JD', '#5b6b8c');
  const agent = { from: joel, agentName: 'Claude Code' };

  const decisions907: InboxAskDto[] = SHIP_907_DECISIONS.map(
    ([question, recommended, alternative], i) =>
      aDecisionAsk(
        { number: i + 1, question, recommended, alternatives: [alternative] },
        { ...agent, atLabel: 'Yesterday' },
      ),
  );

  const items = [
    anInboxItem({
      key: 'SHIP-743',
      type: 'feature',
      title: 'Nightly backups',
      ageLabel: '4d',
      longWait: true,
      asks: [
        aStatusAsk(
          { statusLine: 'Blocked: the host has no pg_dump 18 and no Docker.' },
          { from: sam, atLabel: '4 days ago' },
        ),
      ],
    }),
    anInboxItem({
      key: 'SHIP-892',
      type: 'feature',
      title: 'Demo artifacts',
      ageLabel: '2d',
      longWait: true,
      asks: [
        aQuestionAsk(
          {
            number: 7,
            text: 'What size cap for demo videos? 20 MB keeps attachments light; 50 MB fits a two-minute screen recording.',
            options: ['20 MB', '50 MB'],
          },
          { from: priya, atLabel: '2 days ago' },
        ),
      ],
    }),
    anInboxItem({
      key: 'SHIP-907',
      type: 'feature',
      title: 'Mobile app foundations',
      ageLabel: '1d',
      longWait: false,
      waitedOnBy: [{ key: 'SHIP-906', title: 'Voice commands' }],
      asks: decisions907,
    }),
    anInboxItem({
      key: 'SHIP-924',
      type: 'story',
      title: 'Development build on both test phones',
      underKey: 'SHIP-907',
      ageLabel: '5h',
      longWait: false,
      asks: [
        aQuestionAsk(
          {
            number: 1,
            text: 'The iPhone build needs an Apple developer team. Use your personal team for now, or wait for the ShipLean org account?',
            options: ['Personal team', 'Wait for org'],
          },
          { ...agent, atLabel: '5 hours ago' },
        ),
      ],
    }),
    anInboxItem({
      key: 'SHIP-742',
      type: 'feature',
      title: 'GitHub issues sync',
      ageLabel: '14h',
      longWait: false,
      asks: [
        aMentionAsk(
          {
            excerpt:
              '@Joel lane C needs the GitHub app installed on the org before Thursday’s import test.',
          },
          { from: priya, atLabel: '14 hours ago' },
        ),
      ],
    }),
    anInboxItem({
      key: 'SHIP-906',
      type: 'feature',
      title: 'Voice commands',
      ageLabel: '2h',
      longWait: false,
      asks: [
        aQuestionAsk(
          {
            number: 1,
            text: 'The S22 takes 6.1 s once warm, over the 5 s target. Ship 1.7B as it is, or try FunctionGemma 270M on the S22 first?',
            options: [],
          },
          { ...agent, atLabel: '2 hours ago' },
        ),
        aDecisionAsk(
          {
            number: 10,
            question: 'Download the models on Wi-Fi only, and ask before using mobile data?',
            recommended: 'Wi-Fi only',
            alternatives: ['Any connection'],
          },
          { ...agent, atLabel: '2 hours ago' },
        ),
        aDecisionAsk(
          {
            number: 11,
            question: 'Where does the voice button sit on detail screens?',
            recommended: 'In a dock at the bottom, next to a typed fallback',
            alternatives: ['In the top bar'],
          },
          { ...agent, atLabel: '2 hours ago' },
        ),
      ],
    }),
  ];

  return anInbox({ projectId, items, loadedAtLabel: '09:12' });
}
