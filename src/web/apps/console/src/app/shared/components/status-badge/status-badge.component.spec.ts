import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { StatusBadgeComponent } from './status-badge.component';

describe('StatusBadgeComponent', () => {
  let fixture: ComponentFixture<StatusBadgeComponent>;
  let component: StatusBadgeComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [StatusBadgeComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(StatusBadgeComponent);
    component = fixture.componentInstance;
  });

  it('renders default badge with resolved success variant for Active status', () => {
    fixture.componentRef.setInput('status', 'Active');
    fixture.detectChanges();

    const badgeEl = fixture.nativeElement.querySelector('.nim-status-badge');
    expect(badgeEl).toBeTruthy();
    expect(badgeEl.classList.contains('badge-success')).toBe(true);
    expect(badgeEl.textContent.trim()).toBe('Active');
  });

  it('renders warning variant for Maintenance and Suspended statuses', () => {
    fixture.componentRef.setInput('status', 'Maintenance');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-warning')).toBe(true);

    fixture.componentRef.setInput('status', 'Suspended');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-warning')).toBe(true);
  });

  it('renders danger variant for Inactive and Cancelled statuses', () => {
    fixture.componentRef.setInput('status', 'Inactive');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-danger')).toBe(true);

    fixture.componentRef.setInput('status', 'Cancelled');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-danger')).toBe(true);
  });

  it('renders info variant for InProgress and Submitted statuses', () => {
    fixture.componentRef.setInput('status', 'InProgress');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-info')).toBe(true);
  });

  it('supports explicit variant override', () => {
    fixture.componentRef.setInput('status', 'CustomStatus');
    fixture.componentRef.setInput('variant', 'purple');
    fixture.detectChanges();

    const badgeEl = fixture.nativeElement.querySelector('.nim-status-badge');
    expect(badgeEl.classList.contains('badge-purple')).toBe(true);
  });

  it('supports custom label input', () => {
    fixture.componentRef.setInput('status', 'Active');
    fixture.componentRef.setInput('label', 'Custom Active Label');
    fixture.detectChanges();

    const badgeEl = fixture.nativeElement.querySelector('.nim-status-badge');
    expect(badgeEl.textContent.trim()).toBe('Custom Active Label');
  });

  it('logs console.error and falls back to danger variant for unknown status without explicit variant', () => {
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    fixture.componentRef.setInput('status', 'CompletelyUnknownStatus');
    fixture.detectChanges();

    expect(errorSpy).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.nim-status-badge').classList.contains('badge-danger')).toBe(true);
    errorSpy.mockRestore();
  });
});
