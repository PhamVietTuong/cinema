import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

/**
 * Keys every app shares (shell chrome and the sign-in form). They are merged UNDER each app's own
 * `/assets/i18n/{lang}.json`, so an app can still override any of them by redefining the key.
 */
export const CINEMA_BASE_TRANSLATIONS: Record<string, TranslationObject> = {
  vi: {
    nav: { logout: 'Đăng Xuất' },
    invoices: {
      statusPending: 'Chờ thanh toán',
      statusPaid: 'Đã thanh toán',
      statusCancelled: 'Đã hủy',
      statusFailed: 'Thất bại',
      statusRefunded: 'Đã hoàn tiền',
    },
    staffReason: {
      other: 'Lý do khác',
      customerRequest: 'Khách yêu cầu',
      wrongShowtime: 'Nhầm suất chiếu',
      duplicateSale: 'Bán trùng',
      serviceFailure: 'Sự cố dịch vụ',
      technicalIssue: 'Sự cố kỹ thuật',
      priceMatch: 'Khớp giá',
      compensation: 'Bồi thường',
    },
    tender: {
      cash: 'Tiền mặt',
      card: 'Thẻ',
      qrWallet: 'Ví QR',
      giftCard: 'Thẻ quà tặng',
      points: 'Điểm thưởng',
      online: 'Trực tuyến',
    },
    drawerStatus: {
      open: 'Đang mở',
      closed: 'Đã đóng',
      reconciled: 'Đã đối soát',
    },
    override: {
      title: 'Cần quản lý xác nhận',
      hint: 'Thao tác này cần quản lý xác nhận. Chọn người duyệt và nhập mã PIN của họ.',
      approver: 'Người duyệt',
      pin: 'Mã PIN',
      pinInvalid: 'Mã PIN gồm 4-8 chữ số.',
      noApprovers: 'Không có quản lý nào đang phụ trách rạp này.',
      confirm: 'Xác nhận',
    },
    common: {
      actions: 'Thao Tác',
      all: 'Tất Cả',
      cancel: 'Hủy',
      close: 'Đóng',
      createdAt: 'Ngày Tạo',
      filters: 'Bộ Lọc',
      name: 'Tên',
      remove: 'Xóa',
      required: 'Trường này là bắt buộc.',
      save: 'Lưu',
      status: 'Trạng Thái',
      totalEntries: 'Tổng cộng',
    },
    topbar: {
      lightMode: 'Chế độ sáng',
      darkMode: 'Chế độ tối',
      notifications: 'Thông báo',
    },
    shell: {
      userFallback: 'Người dùng',
      defaultTitle: 'Bảng Điều Khiển',
    },
    login: {
      emailOrPhoneLabel: 'Email hoặc Số Điện Thoại',
      emailOrPhonePlaceholder: 'Nhập email hoặc số điện thoại',
      passwordLabel: 'Mật Khẩu',
      showPassword: 'Hiện mật khẩu',
      hidePassword: 'Ẩn mật khẩu',
      rememberMe: 'Ghi nhớ đăng nhập',
      forgotPassword: 'Quên mật khẩu?',
      submit: 'Đăng Nhập',
    },
  },
  en: {
    nav: { logout: 'Log Out' },
    invoices: {
      statusPending: 'Pending',
      statusPaid: 'Paid',
      statusCancelled: 'Cancelled',
      statusFailed: 'Failed',
      statusRefunded: 'Refunded',
    },
    staffReason: {
      other: 'Other',
      customerRequest: 'Customer request',
      wrongShowtime: 'Wrong showtime',
      duplicateSale: 'Duplicate sale',
      serviceFailure: 'Service failure',
      technicalIssue: 'Technical issue',
      priceMatch: 'Price match',
      compensation: 'Compensation',
    },
    tender: {
      cash: 'Cash',
      card: 'Card',
      qrWallet: 'QR wallet',
      giftCard: 'Gift card',
      points: 'Points',
      online: 'Online',
    },
    drawerStatus: {
      open: 'Open',
      closed: 'Closed',
      reconciled: 'Reconciled',
    },
    override: {
      title: 'Manager approval needed',
      hint: 'This action needs a manager. Pick the approver and enter their PIN.',
      approver: 'Approver',
      pin: 'PIN',
      pinInvalid: 'The PIN has 4-8 digits.',
      noApprovers: 'No manager is assigned to this theater.',
      confirm: 'Approve',
    },
    common: {
      actions: 'Actions',
      all: 'All',
      cancel: 'Cancel',
      close: 'Close',
      createdAt: 'Created At',
      filters: 'Filters',
      name: 'Name',
      remove: 'Remove',
      required: 'This field is required.',
      save: 'Save',
      status: 'Status',
      totalEntries: 'in total',
    },
    topbar: {
      lightMode: 'Light mode',
      darkMode: 'Dark mode',
      notifications: 'Notifications',
    },
    shell: {
      userFallback: 'User',
      defaultTitle: 'Control Panel',
    },
    login: {
      emailOrPhoneLabel: 'Email or Phone Number',
      emailOrPhonePlaceholder: 'Enter email or phone number',
      passwordLabel: 'Password',
      showPassword: 'Show password',
      hidePassword: 'Hide password',
      rememberMe: 'Remember me',
      forgotPassword: 'Forgot password?',
      submit: 'Sign In',
    },
  },
};

function isObject(value: unknown): value is TranslationObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function deepMerge(base: TranslationObject, override: TranslationObject): TranslationObject {
  const result: TranslationObject = { ...base };
  for (const key of Object.keys(override)) {
    const baseValue = base[key];
    const overrideValue = override[key];
    result[key] = isObject(baseValue) && isObject(overrideValue) ? deepMerge(baseValue, overrideValue) : overrideValue;
  }
  return result;
}

/** Loads the app's `/assets/i18n/{lang}.json` and layers it over {@link CINEMA_BASE_TRANSLATIONS}. */
@Injectable()
export class CinemaTranslateLoader extends TranslateLoader {
  private readonly _http = inject(HttpClient);

  getTranslation(lang: string): Observable<TranslationObject> {
    const base = CINEMA_BASE_TRANSLATIONS[lang] ?? {};
    return this._http.get<TranslationObject>(`/assets/i18n/${lang}.json`).pipe(
      map(app => deepMerge(base, app)),
      catchError(() => of(base)),
    );
  }
}
