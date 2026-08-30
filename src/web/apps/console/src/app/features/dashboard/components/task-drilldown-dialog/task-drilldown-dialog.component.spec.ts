import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TaskDrilldownDialogComponent } from './task-drilldown-dialog.component';
import { JobTaskDto } from '../../../../core/api/models/api-models';

describe('TaskDrilldownDialogComponent', () => {
  let component: TaskDrilldownDialogComponent;
  let fixture: ComponentFixture<TaskDrilldownDialogComponent>;

  const mockTasks: JobTaskDto[] = [
    {
      id: 't1',
      ref: 'TSK-001',
      title: 'Auckland CBD Express Delivery',
      status: 'Completed',
      priority: 'High',
      scheduledFor: '2026-08-20T09:30:00Z',
      driverName: 'Dave Smith',
      vehicleRego: 'NZ-101',
      actualDistanceKm: 42,
      createdAt: '2026-08-20T08:00:00Z',
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskDrilldownDialogComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskDrilldownDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should not render dialog backdrop when visible is false', () => {
    fixture.componentRef.setInput('visible', false);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dialog-backdrop')).toBeNull();
  });

  it('should render task details and status badge when visible is true', () => {
    fixture.componentRef.setInput('visible', true);
    fixture.componentRef.setInput('date', '2026-08-20');
    fixture.componentRef.setInput('tasks', mockTasks);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.dialog-backdrop')).toBeTruthy();
    expect(el.querySelector('h3')?.textContent).toContain('2026-08-20 派发任务详情');
    expect(el.querySelector('.cell-title')?.textContent).toContain('Auckland CBD Express Delivery');
    expect(el.querySelector('.status-badge')?.textContent).toContain('已完成');
  });

  it('should emit close event when close button is clicked', () => {
    fixture.componentRef.setInput('visible', true);
    fixture.detectChanges();

    let closed = false;
    component.close.subscribe(() => {
      closed = true;
    });

    const el = fixture.nativeElement as HTMLElement;
    const closeBtn = el.querySelector('.btn-close') as HTMLButtonElement;
    closeBtn.click();
    expect(closed).toBe(true);
  });
});
