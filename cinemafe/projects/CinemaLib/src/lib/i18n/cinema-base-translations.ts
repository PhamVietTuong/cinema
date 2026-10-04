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
    seatMap: {
      screen: 'MÀN HÌNH',
      legendAvailable: 'Ghế Trống',
      legendSelected: 'Đang Chọn',
      legendOccupied: 'Đã Bán',
      legendLocked: 'Đang Giữ',
      legendDouble: 'Ghế Đôi',
      notAvailableForCategory: 'Không khả dụng cho loại vé này',
      capReached: 'Bạn đã chọn đủ số vé — tăng số lượng để chọn thêm',
    },
    ticket: {
      showTime: 'Suất chiếu',
      room: 'Phòng',
      seat: 'Ghế',
      category: 'Loại vé',
      price: 'Giá',
      order: 'Mã đơn',
    },
    priceBreakdown: {
      subtotal: 'Tạm tính',
      total: 'Tổng cộng',
      empty: 'Chưa có mục nào',
    },
    staffEnums: {
      incidentCategory: {
        other: 'Khác', seat: 'Ghế', room: 'Phòng chiếu', projection: 'Máy chiếu',
        sound: 'Âm thanh', safety: 'An toàn', customer: 'Khách hàng', cleanliness: 'Vệ sinh',
      },
      incidentSeverity: { low: 'Thấp', medium: 'Trung bình', high: 'Cao', critical: 'Nghiêm trọng' },
      incidentStatus: { open: 'Đang mở', resolved: 'Đã xử lý' },
      checklistKind: { preShow: 'Trước suất chiếu', postShow: 'Sau suất chiếu' },
      taskStatus: { open: 'Mới', inProgress: 'Đang làm', done: 'Hoàn thành', cancelled: 'Đã hủy' },
      roomStatus: { active: 'Hoạt động', maintenance: 'Bảo trì', inactive: 'Ngừng hoạt động' },
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
    seatMap: {
      screen: 'SCREEN',
      legendAvailable: 'Available',
      legendSelected: 'Selected',
      legendOccupied: 'Sold',
      legendLocked: 'Held',
      legendDouble: 'Double Seat',
      notAvailableForCategory: 'Not available for this ticket type',
      capReached: "You've selected all your tickets — increase a quantity to add more",
    },
    ticket: {
      showTime: 'Showtime',
      room: 'Room',
      seat: 'Seat',
      category: 'Ticket type',
      price: 'Price',
      order: 'Order',
    },
    priceBreakdown: {
      subtotal: 'Subtotal',
      total: 'Total',
      empty: 'Nothing added yet',
    },
    staffEnums: {
      incidentCategory: {
        other: 'Other', seat: 'Seat', room: 'Room', projection: 'Projection',
        sound: 'Sound', safety: 'Safety', customer: 'Customer', cleanliness: 'Cleanliness',
      },
      incidentSeverity: { low: 'Low', medium: 'Medium', high: 'High', critical: 'Critical' },
      incidentStatus: { open: 'Open', resolved: 'Resolved' },
      checklistKind: { preShow: 'Pre-show', postShow: 'Post-show' },
      taskStatus: { open: 'Open', inProgress: 'In progress', done: 'Done', cancelled: 'Cancelled' },
      roomStatus: { active: 'Active', maintenance: 'Maintenance', inactive: 'Inactive' },
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
