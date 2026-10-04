import { BACK_OFFICE_ROLES, NavSection, UserRoles } from 'CinemaLib';

const ADMIN_ONLY: readonly string[] = [UserRoles.Admin];

/**
 * CinemaAdmin sidebar. Items without `titleKey` fall back to the shell's default topbar title.
 * The Warehouse section stays open to every back-office role until it moves to CinemaStaff.
 */
export const ADMIN_MENU: NavSection[] = [
  {
    items: [
      { icon: 'dashboard', labelKey: 'nav.dashboard', route: '/dashboard', titleKey: 'pageTitle.dashboard', roles: ADMIN_ONLY },
      { icon: 'bar_chart', labelKey: 'nav.reports', route: '/reports', titleKey: 'pageTitle.reports', roles: ADMIN_ONLY },
      { icon: 'movie', labelKey: 'nav.movies', route: '/movies', titleKey: 'pageTitle.movies', roles: ADMIN_ONLY },
      { icon: 'event', labelKey: 'nav.showtimes', route: '/showtimes', titleKey: 'pageTitle.showtimes', roles: ADMIN_ONLY },
      { icon: 'theaters', labelKey: 'nav.theaters', route: '/theaters', titleKey: 'pageTitle.theaters', roles: ADMIN_ONLY },
      { icon: 'group', labelKey: 'nav.users', route: '/users', titleKey: 'pageTitle.users', roles: ADMIN_ONLY },
    ],
  },
  {
    titleKey: 'section.catalog',
    items: [
      { icon: 'category', labelKey: 'nav.movieTypes', route: '/movie-types', titleKey: 'pageTitle.movieTypes', roles: ADMIN_ONLY },
      { icon: 'shield', labelKey: 'nav.ageRestrictions', route: '/age-restrictions', titleKey: 'pageTitle.ageRestrictions', roles: ADMIN_ONLY },
      { icon: 'sell', labelKey: 'nav.discountTypes', route: '/discount-types', titleKey: 'pageTitle.discountTypes', roles: ADMIN_ONLY },
      { icon: 'card_membership', labelKey: 'nav.memberships', route: '/memberships', titleKey: 'pageTitle.memberships', roles: ADMIN_ONLY },
      { icon: 'badge', labelKey: 'nav.userTypes', route: '/user-types', titleKey: 'pageTitle.userTypes', roles: ADMIN_ONLY },
      { icon: 'celebration', labelKey: 'nav.holidays', route: '/holidays', titleKey: 'pageTitle.holidays', roles: ADMIN_ONLY },
      { icon: 'article', labelKey: 'nav.news', route: '/news', titleKey: 'pageTitle.news', roles: ADMIN_ONLY },
    ],
  },
  {
    titleKey: 'section.operations',
    items: [
      { icon: 'local_offer', labelKey: 'nav.discounts', route: '/discounts', titleKey: 'pageTitle.discounts', roles: ADMIN_ONLY },
      { icon: 'receipt_long', labelKey: 'nav.invoices', route: '/invoices', titleKey: 'pageTitle.invoices', roles: ADMIN_ONLY },
      { icon: 'forum', labelKey: 'nav.comments', route: '/comments', roles: ADMIN_ONLY },
      { icon: 'card_giftcard', labelKey: 'nav.giftCards', route: '/gift-cards', roles: ADMIN_ONLY },
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
