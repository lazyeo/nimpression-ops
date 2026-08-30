import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OdometerTrendChartComponent } from './odometer-trend-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('OdometerTrendChartComponent', () => {
  let component: OdometerTrendChartComponent;
  let fixture: ComponentFixture<OdometerTrendChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OdometerTrendChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(OdometerTrendChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render header title', () => {
    expect(component).toBeTruthy();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.card-title')?.textContent).toBeTruthy();
  });
});
