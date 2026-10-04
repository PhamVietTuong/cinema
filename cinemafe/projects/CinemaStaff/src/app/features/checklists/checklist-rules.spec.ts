import { canComplete, checklistProgress, missingRequired, moveItem } from './checklist-rules';

describe('checklist rules', () => {
  const items = [
    { id: 'a', isRequired: true, isDone: true },
    { id: 'b', isRequired: true, isDone: false },
    { id: 'c', isRequired: false, isDone: false },
  ];

  it('lists only required items that are not done', () => {
    expect(missingRequired(items).map(i => i.id)).toEqual(['b']);
  });

  it('blocks completion while a required item is open', () => {
    expect(canComplete(items)).toBe(false);
  });

  it('allows completion when only optional items are open', () => {
    expect(canComplete([{ isRequired: true, isDone: true }, { isRequired: false, isDone: false }])).toBe(true);
  });

  it('allows completing an empty checklist', () => {
    expect(canComplete([])).toBe(true);
  });

  it('computes progress', () => {
    expect(checklistProgress(items)).toEqual({ done: 1, total: 3, percent: 33 });
    expect(checklistProgress([]).percent).toBe(100);
  });

  it('moves an item and ignores invalid moves', () => {
    expect(moveItem([1, 2, 3], 0, 2)).toEqual([2, 3, 1]);
    expect(moveItem([1, 2, 3], 2, 1)).toEqual([1, 3, 2]);
    expect(moveItem([1, 2, 3], 0, 5)).toEqual([1, 2, 3]);
  });
});
