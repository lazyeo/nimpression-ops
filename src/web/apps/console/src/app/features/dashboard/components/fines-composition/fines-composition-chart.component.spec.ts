import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FinesCompositionChartComponent } from './fines-composition-chart.component';

if (typeof window !== 'undefined' && !window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

describe('FinesCompositionChartComponent', () => {
  let component: FinesCompositionChartComponent;
  let fixture: ComponentFixture<FinesCompositionChartComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FinesCompositionChartComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(FinesCompositionChartComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and handle sector click for category filtering', () => {
    let selected: string | null = null;
    component.categorySelect.subscribe((cat) => {
      selected = cat;
    });

    component.onDoughnutClick({ name: 'Speeding' } as any);
    expect(selected).toBe('Speeding');
  });

  it('should clear category filter when clear button is clicked', () => {
    fixture.componentRef.setInput('selectedCategory', 'Speeding');
    fixture.detectChanges();

    let selected: string | null = 'init';
    component.categorySelect.subscribe((cat) => {
      selected = cat;
    });

    const el = fixture.nativeElement as HTMLElement;
    const clearBtn = el.querySelector('.btn-clear-tag') as HTMLButtonElement;
    clearBtn.click();
    expect(selected).toBeNull();
  });
});
