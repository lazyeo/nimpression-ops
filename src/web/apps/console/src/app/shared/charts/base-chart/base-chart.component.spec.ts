import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BaseChartComponent } from './base-chart.component';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('BaseChartComponent', () => {
  let component: BaseChartComponent;
  let fixture: ComponentFixture<BaseChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BaseChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(BaseChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the base chart component', () => {
    expect(component).toBeTruthy();
  });

  it('should render skeleton loading overlay when loading is true', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const skeleton = element.querySelector('nim-chart-skeleton');
    expect(skeleton).toBeTruthy();
  });

  it('should render error overlay with retry button when error is present', () => {
    fixture.componentRef.setInput('error', 'Network Timeout');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const errorTitle = element.querySelector('.error-title');
    const errorMessage = element.querySelector('.error-message');
    const retryBtn = element.querySelector('.btn-retry');

    expect(errorTitle?.textContent).toContain('图表加载失败');
    expect(errorMessage?.textContent).toContain('Network Timeout');
    expect(retryBtn).toBeTruthy();

    let retried = false;
    component.retry.subscribe(() => {
      retried = true;
    });

    (retryBtn as HTMLButtonElement).click();
    expect(retried).toBe(true);
  });

  it('should render empty state when isEmpty is true', () => {
    fixture.componentRef.setInput('isEmpty', true);
    fixture.componentRef.setInput('emptyText', '暂无车辆数据');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const emptyText = element.querySelector('.empty-text');
    expect(emptyText?.textContent).toContain('暂无车辆数据');
  });

  it('should render echarts host element when options are provided', () => {
    fixture.componentRef.setInput('options', {
      xAxis: { type: 'category', data: ['A', 'B'] },
      yAxis: { type: 'value' },
      series: [{ type: 'bar', data: [1, 2] }],
    });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const echartsHost = element.querySelector('.echarts-host');
    expect(echartsHost).toBeTruthy();
  });
});
