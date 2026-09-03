import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FleetUtilizationChartComponent } from './fleet-utilization-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('FleetUtilizationChartComponent', () => {
  let component: FleetUtilizationChartComponent;
  let fixture: ComponentFixture<FleetUtilizationChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FleetUtilizationChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FleetUtilizationChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component', () => {
    expect(component).toBeTruthy();
  });

  it('should emit dayClick event on chart click', () => {
    let clickedDate = '';
    component.dayClick.subscribe((date) => {
      clickedDate = date;
    });

    component.onChartClick({ name: '2026-08-20' } as any);
    expect(clickedDate).toBe('2026-08-20');
  });
});
