/** Pure rules for running a checklist; they mirror what the API enforces on CompleteChecklist. */

export interface ChecklistItemState {
  isRequired?: boolean;
  isDone?: boolean;
}

/** Required items that are not ticked yet. */
export function missingRequired<T extends ChecklistItemState>(items: readonly T[]): T[] {
  return items.filter(item => item.isRequired && !item.isDone);
}

/** A checklist can be completed once every required item is done (optional items may stay open). */
export function canComplete(items: readonly ChecklistItemState[]): boolean {
  return missingRequired(items).length === 0;
}

export interface ChecklistProgress {
  done: number;
  total: number;
  /** Whole percent 0..100; an empty list counts as 100. */
  percent: number;
}

export function checklistProgress(items: readonly ChecklistItemState[]): ChecklistProgress {
  const total = items.length;
  const done = items.filter(item => item.isDone).length;
  return { done, total, percent: total === 0 ? 100 : Math.round((done / total) * 100) };
}

/** Moves the item at `from` to `to` (a new array); out-of-range moves return the list unchanged. */
export function moveItem<T>(items: readonly T[], from: number, to: number): T[] {
  const copy = [...items];
  if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) {
    return copy;
  }
  const [moved] = copy.splice(from, 1);
  copy.splice(to, 0, moved);
  return copy;
}
