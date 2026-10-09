import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '@ngx-translate/core';

/** One option of a `select` filter. Give either a translated `labelKey` or a literal `label`. */
export interface FilterBarOption {
  value: string;
  labelKey?: string;
  label?: string;
}

/** One control of the filter bar, bound to the form control named `key`. */
export interface FilterBarField {
  key: string;
  type: 'text' | 'select' | 'multiselect' | 'toggle' | 'date';
  /** i18n key of the control label. */
  labelKey: string;
  /** `select` only: the choices. */
  options?: readonly FilterBarOption[];
  /** `select` only: i18n key of the empty "no filter" option (default `common.all`). */
  allLabelKey?: string;
  /** `select` only: false removes the empty "no filter" option, so a choice is always made. Default true. */
  allowEmpty?: boolean;
}

/**
 * Filter card above a paged list. It renders the `fields` against the page's `searchForm` and emits
 * `filtersChange` after every edit; the page then reloads (the table base class debounces it).
 *
 * Usage: `<cl-filter-bar [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />`
 */
@Component({
  selector: 'cl-filter-bar',
  standalone: true,
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSlideToggleModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="ad-filter-card">
      <mat-card-content>
        <h3 class="ad-card-title">{{ titleKey() | translate }}</h3>
        <form [formGroup]="form()" class="ad-filter-grid">
          @for (field of fields(); track field.key) {
            @switch (field.type) {
              @case ('text') {
                <mat-form-field appearance="outline">
                  <mat-label>{{ field.labelKey | translate }}</mat-label>
                  <input matInput [formControlName]="field.key" (input)="filtersChange.emit()">
                </mat-form-field>
              }
              @case ('date') {
                <mat-form-field appearance="outline">
                  <mat-label>{{ field.labelKey | translate }}</mat-label>
                  <input matInput type="date" [formControlName]="field.key" (change)="filtersChange.emit()">
                </mat-form-field>
              }
              @case ('select') {
                <mat-form-field appearance="outline">
                  <mat-label>{{ field.labelKey | translate }}</mat-label>
                  <mat-select [formControlName]="field.key" (selectionChange)="filtersChange.emit()">
                    @if (field.allowEmpty !== false) {
                      <mat-option value="">{{ (field.allLabelKey ?? 'common.all') | translate }}</mat-option>
                    }
                    @for (option of field.options ?? []; track option.value) {
                      <mat-option [value]="option.value">{{ option.labelKey ? (option.labelKey | translate) : option.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              }
              @case ('multiselect') {
                <mat-form-field appearance="outline">
                  <mat-label>{{ field.labelKey | translate }}</mat-label>
                  <mat-select multiple [formControlName]="field.key" (selectionChange)="filtersChange.emit()">
                    @for (option of field.options ?? []; track option.value) {
                      <mat-option [value]="option.value">{{ option.labelKey ? (option.labelKey | translate) : option.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              }
              @case ('toggle') {
                <div class="cl-filter-toggle">
                  <mat-slide-toggle [formControlName]="field.key" (change)="filtersChange.emit()">{{ field.labelKey | translate }}</mat-slide-toggle>
                </div>
              }
            }
          }
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .cl-filter-toggle { display: flex; align-items: center; padding-bottom: 8px; }
  `],
})
export class FilterBarComponent {
  /** The page's search form; it must contain a control for every field key. */
  readonly form = input.required<FormGroup>();
  readonly fields = input.required<readonly FilterBarField[]>();
  /** i18n key of the card heading. */
  readonly titleKey = input<string>('common.filters');
  /** Emitted after the user edits any control. */
  readonly filtersChange = output<void>();
}
