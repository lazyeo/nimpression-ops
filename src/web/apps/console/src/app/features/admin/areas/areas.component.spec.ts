import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { AreasComponent } from './areas.component';
import { AreasService } from './services/areas.service';
import { AuthService } from '../../../core/auth/auth.service';
import { AreaAssignmentDto, AreaDto, PaginatedResult } from './models/areas.models';

describe('AreasComponent', () => {
  let component: AreasComponent;
  let fixture: ComponentFixture<AreasComponent>;
  let areasServiceMock: {
    getAreas: ReturnType<typeof vi.fn>;
    getAreaById: ReturnType<typeof vi.fn>;
    createArea: ReturnType<typeof vi.fn>;
    updateArea: ReturnType<typeof vi.fn>;
    deleteArea: ReturnType<typeof vi.fn>;
    assignDriverToArea: ReturnType<typeof vi.fn>;
    endAreaAssignment: ReturnType<typeof vi.fn>;
    getAreaAssignments: ReturnType<typeof vi.fn>;
    getAllAreaAssignments: ReturnType<typeof vi.fn>;
    getDrivers: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    isAdminOrDispatcher: ReturnType<typeof vi.fn>;
  };

  const mockAreas: AreaDto[] = [
    {
      id: 'area-1',
      name: 'Auckland Central',
      code: 'AKL-CBD',
      description: 'Central Commercial District',
      isActive: true,
    },
    {
      id: 'area-2',
      name: 'Manukau South',
      code: 'MNK-STH',
      description: 'South Auckland Hub',
      isActive: true,
    },
  ];

  const mockAssignments: AreaAssignmentDto[] = [
    {
      id: 'asg-1',
      areaId: 'area-1',
      areaName: 'Auckland Central',
      areaCode: 'AKL-CBD',
      driverId: 'drv-1',
      driverName: 'John Driver',
      driverEmployeeNo: 'DRV-1001',
      effectiveFrom: '2026-01-01',
      isActive: true,
    },
  ];

  const mockPaginatedResult: PaginatedResult<AreaDto> = {
    items: mockAreas,
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    areasServiceMock = {
      getAreas: vi.fn().mockReturnValue(of(mockPaginatedResult)),
      getAreaById: vi.fn().mockReturnValue(of({ ...mockAreas[0], activeDriversCount: 1 })),
      createArea: vi.fn().mockReturnValue(of(mockAreas[0])),
      updateArea: vi.fn().mockReturnValue(of(mockAreas[0])),
      deleteArea: vi.fn().mockReturnValue(of(undefined)),
      assignDriverToArea: vi.fn().mockReturnValue(of(mockAssignments[0])),
      endAreaAssignment: vi.fn().mockReturnValue(of(mockAssignments[0])),
      getAreaAssignments: vi.fn().mockReturnValue(of(mockAssignments)),
      getAllAreaAssignments: vi.fn().mockReturnValue(of(mockAssignments)),
      getDrivers: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
    };

    authServiceMock = {
      isAdminOrDispatcher: vi.fn().mockReturnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [AreasComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AreasService, useValue: areasServiceMock },
        { provide: AuthService, useValue: authServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AreasComponent);
    component = fixture.componentInstance;
  });

  it('should render operational areas list in success state', () => {
    fixture.detectChanges();

    expect(component.state()).toBe('success');
    expect(component.areas().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.data-table')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
    expect(compiled.textContent).toContain('AKL-CBD');
    expect(compiled.textContent).toContain('Auckland Central');
    expect(compiled.textContent).toContain('MNK-STH');
  });

  it('should render empty state when no areas exist', () => {
    areasServiceMock.getAreas.mockReturnValue(
      of({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      }),
    );

    fixture.detectChanges();

    expect(component.state()).toBe('empty');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.state-card')).toBeTruthy();
  });

  it('should filter areas on search input', () => {
    fixture.detectChanges();

    component.onSearchInput({ target: { value: 'Manukau' } } as unknown as Event);
    expect(component.searchTerm()).toBe('Manukau');
    expect(areasServiceMock.getAreas).toHaveBeenCalledWith(
      expect.objectContaining({ searchTerm: 'Manukau' }),
    );
  });

  it('should render error state with retry button on failure', () => {
    const errorResponse = new HttpErrorResponse({
      status: 500,
      error: { message: 'Area service error' },
    });
    areasServiceMock.getAreas.mockReturnValue(throwError(() => errorResponse));

    fixture.detectChanges();

    expect(component.state()).toBe('error');
    const compiled = fixture.nativeElement as HTMLElement;
    const retryBtn = compiled.querySelector('.state-card button') as HTMLButtonElement;
    expect(retryBtn).toBeTruthy();

    areasServiceMock.getAreas.mockReturnValue(of(mockPaginatedResult));
    retryBtn.click();
    expect(areasServiceMock.getAreas).toHaveBeenCalledTimes(2);
  });

  it('should validate and create new operational area', () => {
    fixture.detectChanges();

    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.createForm.code = 'WLG-CBD';
    component.createForm.name = 'Wellington Central';
    component.createForm.description = 'Capital region hub';

    component.submitCreateArea();

    expect(areasServiceMock.createArea).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 'WLG-CBD',
        name: 'Wellington Central',
      }),
    );
    expect(component.isCreateModalOpen()).toBe(false);
  });

  it('should display conflict interval when driver area assignment returns 422 overlap', () => {
    fixture.detectChanges();

    component.openAssignModal(mockAreas[0]);
    component.assignForm.driverId = 'drv-1';
    component.assignForm.effectiveFrom = '2026-03-01';

    const overlapError = new HttpErrorResponse({
      status: 422,
      statusText: 'Unprocessable Entity',
      error: {
        message: 'Driver area assignment dates overlap with existing assignment (2026-01-01 to 2026-06-30).',
      },
    });
    areasServiceMock.assignDriverToArea.mockReturnValue(throwError(() => overlapError));

    component.submitAssignDriver();

    expect(component.formError()).toContain('overlap with existing assignment');
    expect(component.isAssignModalOpen()).toBe(true);
  });
});
