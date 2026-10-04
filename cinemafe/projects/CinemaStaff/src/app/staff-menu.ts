import { APPROVER_ROLES, BACK_OFFICE_ROLES, GATE_KEEPER_ROLES, NavSection, REPORTING_ROLES, SELLER_ROLES, CONCESSION_ROLES } from 'CinemaLib';
import { LOW_STOCK_BADGE } from './core/staff-live.service';

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
    titleKey: 'gate.section',
    items: [
      { icon: 'qr_code_scanner', labelKey: 'gate.nav.gate', route: '/gate', titleKey: 'gate.pageTitle', roles: GATE_KEEPER_ROLES },
    ],
  },
  {
    titleKey: 'kitchen.section',
    items: [
      { icon: 'restaurant', labelKey: 'kitchen.nav.kitchen', route: '/kitchen', titleKey: 'kitchen.pageTitle', roles: CONCESSION_ROLES, badge: LOW_STOCK_BADGE },
    ],
  },
  {
    titleKey: 'salesReports.section',
    items: [
      { icon: 'insights', labelKey: 'salesReports.nav.reports', route: '/reports', titleKey: 'salesReports.pageTitle', roles: REPORTING_ROLES },
    ],
  },
  {
    titleKey: 'auditLog.section',
    items: [
      { icon: 'fact_check', labelKey: 'auditLog.nav.auditLog', route: '/audit-log', titleKey: 'auditLog.pageTitle', roles: REPORTING_ROLES },
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
  {
    titleKey: 'pos.section',
    items: [
      { icon: 'point_of_sale', labelKey: 'pos.nav.counter', route: '/pos', titleKey: 'pos.pageTitle.counter', roles: SELLER_ROLES },
      { icon: 'savings', labelKey: 'drawer.nav.drawer', route: '/drawer', titleKey: 'drawer.pageTitle.drawer', roles: SELLER_ROLES },
    ],
  },
];
