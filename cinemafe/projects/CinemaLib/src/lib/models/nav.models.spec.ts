import { NavSection, filterNavByRole } from './nav.models';

describe('filterNavByRole', () => {
  const menu: NavSection[] = [
    { items: [{ icon: 'a', labelKey: 'a', route: '/a', roles: ['Admin'] }, { icon: 'b', labelKey: 'b', route: '/b' }] },
    { titleKey: 'section.x', items: [{ icon: 'c', labelKey: 'c', route: '/c', roles: ['Admin'] }] },
  ];

  it('keeps every item for a permitted role', () => {
    const result = filterNavByRole(menu, 'Admin');
    expect(result.map(s => s.items.length)).toEqual([2, 1]);
  });

  it('drops restricted items and keeps unrestricted ones', () => {
    const result = filterNavByRole(menu, 'TheaterStaff');
    expect(result.length).toBe(1);
    expect(result[0].items.map(i => i.route)).toEqual(['/b']);
  });

  it('drops a section left with no items, together with its title', () => {
    expect(filterNavByRole(menu, 'TheaterStaff').some(s => s.titleKey === 'section.x')).toBe(false);
  });

  it('treats a missing role as no role', () => {
    expect(filterNavByRole(menu, null)[0].items.map(i => i.route)).toEqual(['/b']);
  });
});
