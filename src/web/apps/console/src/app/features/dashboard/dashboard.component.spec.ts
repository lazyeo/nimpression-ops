import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardComponent } from './dashboard.component';
import { DashboardDataService } from './services/dashboard-data.service';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let dataService: DashboardDataService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [DashboardDataService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    dataService = TestBed.inject(DashboardDataService);
    fixture.detectChanges();
  });

  it('should create the dashboard component and initialize data load', () => {
    expect(component).toBeTruthy();
  });

  it('should render all 6 chart components', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('nim-fleet-utilization-chart')).toBeTruthy();
    expect(el.querySelector('nim-timesheet-heatmap-chart')).toBeTruthy();
    expect(el.querySelector('nim-odometer-trend-chart')).toBeTruthy();
    expect(el.querySelector('nim-fines-composition-chart')).toBeTruthy();
    expect(el.querySelector('nim-task-funnel-chart')).toBeTruthy();
    expect(el.querySelector('nim-payroll-comparison-chart')).toBeTruthy();
  });

  it('should open and close task drilldown dialog', () => {
    expect(component.drilldownVisible()).toBe(false);

    component.onFleetDayDrilldown('2026-08-20');
    fixture.detectChanges();

    expect(component.drilldownVisible()).toBe(true);
    expect(component.drilldownDate()).toBe('2026-08-20');

    component.closeDrilldown();
    fixture.detectChanges();
    expect(component.drilldownVisible()).toBe(false);
  });

  it('should toggle theme when theme button is clicked', () => {
    expect(dataService.theme().name).toBe('light');
    component.toggleTheme();
    expect(dataService.theme().name).toBe('dark');
  });
});
