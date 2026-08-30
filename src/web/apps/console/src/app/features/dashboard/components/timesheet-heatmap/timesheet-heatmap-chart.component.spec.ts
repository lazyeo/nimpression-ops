import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimesheetHeatmapChartComponent } from './timesheet-heatmap-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('TimesheetHeatmapChartComponent', () => {
  let component: TimesheetHeatmapChartComponent;
  let fixture: ComponentFixture<TimesheetHeatmapChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimesheetHeatmapChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimesheetHeatmapChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render header title', () => {
    expect(component).toBeTruthy();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.card-title')?.textContent).toBeTruthy();
  });
});
