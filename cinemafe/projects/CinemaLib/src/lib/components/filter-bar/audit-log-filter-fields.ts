import { AuditActionValues } from '../../interfaces/cinema.model';
import { FilterBarField } from './filter-bar.component';

/** Action/date-range filters for the audit log, shared by CinemaStaff's and CinemaAdmin's pages. */
export const AUDIT_LOG_FILTER_FIELDS: readonly FilterBarField[] = [
  {
    key: 'action',
    type: 'select',
    labelKey: 'auditLog.col.action',
    options: AuditActionValues.map(a => ({ value: String(a.value), labelKey: a.name })),
  },
  { key: 'from', type: 'date', labelKey: 'auditLog.from' },
  { key: 'to', type: 'date', labelKey: 'auditLog.to' },
];
