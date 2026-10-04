import { BACK_OFFICE_ROLES, SELLER_ROLES, NavSection } from 'CinemaLib';

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
