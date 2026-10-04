import { Signal } from '@angular/core';

/** One link in a sidebar menu. */
export interface NavItem {
  /** Material icon name. */
  icon: string;
  /** i18n key of the link label. */
  labelKey: string;
  /** Router link (absolute, e.g. '/dashboard'). */
  route: string;
  /** Roles that may see this link. Omitted = every signed-in user. */
  roles?: readonly string[];
  /** i18n key of the topbar title shown while this link's first URL segment is active. */
  titleKey?: string;
  /** Optional counter bubble; hidden while it is 0. */
  badge?: Signal<number>;
}

/** A group of links under an optional divider label. */
export interface NavSection {
  /** i18n key of the divider label. Omitted = no label (e.g. the first group). */
  titleKey?: string;
  items: NavItem[];
}

/**
 * Returns the menu a given role may see: items the role may not open are dropped,
 * and a section left with no items disappears together with its divider label.
 */
export function filterNavByRole(menu: readonly NavSection[], role: string | null | undefined): NavSection[] {
  const userRole = role ?? '';
  return menu
    .map(section => ({
      ...section,
      items: section.items.filter(item => !item.roles || item.roles.includes(userRole)),
    }))
    .filter(section => section.items.length > 0);
}
