import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TaskFunnelChartComponent } from './task-funnel-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('TaskFunnelChartComponent', () => {
  let component: TaskFunnelChartComponent;
  let fixture: ComponentFixture<TaskFunnelChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskFunnelChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskFunnelChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render header title', () => {
    expect(component).toBeTruthy();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.card-title')?.textContent).toBeTruthy();
  });
});
