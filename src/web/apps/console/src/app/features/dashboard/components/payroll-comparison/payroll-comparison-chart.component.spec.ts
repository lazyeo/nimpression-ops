import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PayrollComparisonChartComponent } from './payroll-comparison-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('PayrollComparisonChartComponent', () => {
  let component: PayrollComparisonChartComponent;
  let fixture: ComponentFixture<PayrollComparisonChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PayrollComparisonChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PayrollComparisonChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render header title', () => {
    expect(component).toBeTruthy();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.card-title')?.textContent).toContain('薪资对比分析');
  });
});
