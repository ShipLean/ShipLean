import { describe, expect, it } from 'vitest';
import { editDistance, findPerson, findState, plain } from './fuzzy';

const people = [
  { id: 'u1', name: 'Priya Shah' },
  { id: 'u2', name: 'José Núñez' },
  { id: 'u3', name: 'Tom Reed' },
  { id: 'u4', name: 'Tom Hall' },
];
const featureStates = [
  { id: 'f1', name: 'Proposed' },
  { id: 'f2', name: 'Active' },
  { id: 'f3', name: 'Resolved' },
  { id: 'f4', name: 'Closed' },
];

describe('fuzzy', () => {
  it('compares without case, accents, hyphens or extra spaces', () => {
    expect(plain('  José  Núñez-Ruiz ')).toBe('jose nunez ruiz');
    expect(editDistance('priya', 'pria')).toBe(1);
  });
  it('finds a person by surname, without accents, or with one slip', () => {
    expect(findPerson('Shah', people)).toBe(people[0]);
    expect(findPerson('jose nunez', people)).toBe(people[1]);
    expect(findPerson('Nunez', people)).toBe(people[1]);
    expect(findPerson('Pria', people)).toBe(people[0]);
    expect(findPerson('Priya Shaw', people)).toBe(people[0]);
  });
  it('prefers an exact surname over a near one', () => {
    const kents = [
      { id: 'u1', name: 'Clark Kent' },
      { id: 'u2', name: 'Immanuel Kant' },
    ];
    expect(findPerson('Kent', kents)).toBe(kents[0]);
  });
  it('prefers an exact first name over a near one', () => {
    const anns = [
      { id: 'u1', name: 'Ann Lee' },
      { id: 'u2', name: 'Anna Ng' },
    ];
    expect(findPerson('Ann', anns)).toBe(anns[0]);
  });
  it('finds nobody for a shared first name, a stranger, or a short name with a slip', () => {
    expect(findPerson('Tom', people)).toBeNull();
    expect(findPerson('Ana', people)).toBeNull();
    expect(findPerson('Tim', people)).toBeNull();
  });
  it('finds a state by another form of its words, and none the type lacks', () => {
    expect(findState('resolve', featureStates)).toBe(featureStates[2]);
    expect(findState('close', featureStates)).toBe(featureStates[3]);
    expect(findState('propose', featureStates)).toBe(featureStates[0]);
    expect(findState('Done', featureStates)).toBeNull();
  });
});
