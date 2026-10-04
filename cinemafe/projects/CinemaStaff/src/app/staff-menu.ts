import { APPROVER_ROLES, BACK_OFFICE_ROLES, NavSection } from 'CinemaLib';

/**
 * CinemaStaff sidebar. Each item may carry `roles` (omitted = every staff role); the shared
 * shell hides what the signed-in role may not open. Later phases append their sections here.
 */
export const STAFF_MENU: NavSection[] = [
  {
    items: [
      { icon: 'home', labelKey: 'nav.home', route: '/home', titleKey: 'pageTitle.home' },
    ],
  },
  {
    titleKey: 'schedule.section',
    items: [
      { icon: 'calendar_view_day', labelKey: 'schedule.nav', route: '/schedule', titleKey: 'schedule.title' },
      { icon: 'report', labelKey: 'incidents.nav', route: '/incidents', titleKey: 'incidents.list.title' },
      { icon: 'fact_check', labelKey: 'checklists.nav', route: '/checklists', titleKey: 'checklists.title' },
    ],
  },
  {
    titleKey: 'timeClock.section',
    items: [
      { icon: 'schedule', labelKey: 'timeClock.nav', route: '/time-clock', titleKey: 'timeClock.title' },
      { icon: 'task_alt', labelKey: 'tasks.nav', route: '/tasks', titleKey: 'tasks.my.title' },
      { icon: 'calendar_month', labelKey: 'roster.nav', route: '/roster', titleKey: 'roster.title', roles: APPROVER_ROLES },
      { icon: 'assignment_ind', labelKey: 'tasks.boardNav', route: '/tasks/board', titleKey: 'tasks.board.title', roles: APPROVER_ROLES },
    ],
  },
  {
    titleKey: 'warehouse.section',
    items: [
      { icon: 'inventory_2', labelKey: 'warehouse.nav.inventory', route: '/inventory', titleKey: 'warehouse.pageTitle.inventory', roles: BACK_OFFICE_ROLES },
      { icon: 'assignment', labelKey: 'warehouse.nav.storagePlans', route: '/storage-plans', titleKey: 'warehouse.pageTitle.storagePlans', roles: BACK_OFFICE_ROLES },
    ],
  },
];
