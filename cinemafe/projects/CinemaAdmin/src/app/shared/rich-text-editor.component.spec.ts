import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Component } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { RichTextEditorComponent } from './rich-text-editor.component';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RichTextEditorComponent],
  template: '<app-rich-text-editor [formControl]="ctrl"></app-rich-text-editor>',
})
class HostComponent {
  ctrl = new FormControl('<b>Hi</b>');
}

describe('RichTextEditorComponent', () => {
  it('writes the control value into the editable area and reports edits back', () => {
    const fixture = TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideTranslateService()] }).createComponent(HostComponent);
    fixture.detectChanges();
    const area: HTMLElement = fixture.nativeElement.querySelector('.rte__area');
    expect(area.innerHTML).toBe('<b>Hi</b>');

    area.innerHTML = '<i>Yo</i>';
    area.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.ctrl.value).toBe('<i>Yo</i>');

    area.innerHTML = '<br>';
    area.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.ctrl.value).toBe('');
  });
});
