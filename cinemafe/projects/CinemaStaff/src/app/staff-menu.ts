import { APPROVER_ROLES, GATE_KEEPER_ROLES, NavSection, REPORTING_ROLES, SELLER_ROLES } from 'CinemaLib';

/**
 * CinemaStaff sidebar. Each item may carry `roles` (omitted = every staff role); the shared
 * shell hides what the signed-in role may not open. Later phases append their sections here.
 *
 * Kitchen (food pickup queue, /kitchen) is intentionally not listed here for now — the feature
 * and its route stay in place, just hidden from the sidebar.
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
    titleKey: 'roster.section',
    items: [
      { icon: 'calendar_month', labelKey: 'roster.nav', route: '/roster', titleKey: 'roster.title', roles: APPROVER_ROLES },
    ],
  },
  {
    titleKey: 'pos.section',
    items: [
      { icon: 'point_of_sale', labelKey: 'pos.nav.counter', route: '/pos', titleKey: 'pos.pageTitle.counter', roles: SELLER_ROLES },
    ],
  },
];
