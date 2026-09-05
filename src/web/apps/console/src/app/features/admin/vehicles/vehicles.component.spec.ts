import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { VehiclesComponent } from './vehicles.component';
import { VehiclesService } from './services/vehicles.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { PaginatedResult, VehicleDetailDto, VehicleSummaryDto } from './models/vehicles.models';

describe('VehiclesComponent', () => {
  let component: VehiclesComponent;
  let fixture: ComponentFixture<VehiclesComponent>;
  let vehiclesServiceMock: {
    getVehicles: ReturnType<typeof vi.fn>;
    getVehicleById: ReturnType<typeof vi.fn>;
    createVehicle: ReturnType<typeof vi.fn>;
    updateVehicle: ReturnType<typeof vi.fn>;
    updateVehicleStatus: ReturnType<typeof vi.fn>;
    recordService: ReturnType<typeof vi.fn>;
    assignVehicle: ReturnType<typeof vi.fn>;
    releaseAssignment: ReturnType<typeof vi.fn>;
    getActiveAssignment: ReturnType<typeof vi.fn>;
    getVehicleAssignments: ReturnType<typeof vi.fn>;
    recordOdometerReading: ReturnType<typeof vi.fn>;
    getOdometerReadings: ReturnType<typeof vi.fn>;
    getDrivers: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    isAdminOrDispatcher: ReturnType<typeof vi.fn>;
    isAdmin: ReturnType<typeof vi.fn>;
  };

  const mockVehicles: VehicleSummaryDto[] = [
    {
      id: 'veh-1',
      rego: 'ABC123',
      make: 'Toyota',
      model: 'HiAce',
      year: 2023,
      odometerKm: 32000,
      serviceIntervalKm: 10000,
      lastServiceOdometerKm: 30000,
      distanceSinceLastServiceKm: 2000,
      isServiceDue: false,
      wofExpiry: '2026-11-20',
      status: 'Active',
      currentDriverId: 'drv-1',
      currentDriverName: 'John Driver',
    },
    {
      id: 'veh-2',
      rego: 'XYZ999',
      make: 'Ford',
      model: 'Transit',
      year: 2022,
      odometerKm: 65000,
      serviceIntervalKm: 10000,
      lastServiceOdometerKm: 50000,
      distanceSinceLastServiceKm: 15000,
      isServiceDue: true,
      wofExpiry: '2026-09-10',
      status: 'Maintenance',
    },
  ];

  const mockDetail: VehicleDetailDto = {
    ...mockVehicles[0],
    vinEnc: '17CHARACTERSVIN12',
    activeAssignment: {
      id: 'asg-1',
      vehicleId: 'veh-1',
      driverId: 'drv-1',
      driverName: 'John Driver',
      assignedAt: '2026-01-01T00:00:00Z',
      assignedByUserId: 'usr-1',
      isActive: true,
    },
    latestOdometerReading: {
      id: 'odo-1',
      vehicleId: 'veh-1',
      driverId: 'drv-1',
      readingKm: 32000,
      recordedAt: '2026-09-01T00:00:00Z',
      source: 'DriverApp',
    },
  };

  const mockPaginatedResult: PaginatedResult<VehicleSummaryDto> = {
    items: mockVehicles,
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    vehiclesServiceMock = {
      getVehicles: vi.fn().mockReturnValue(of(mockPaginatedResult)),
      getVehicleById: vi.fn().mockReturnValue(of(mockDetail)),
      createVehicle: vi.fn().mockReturnValue(of('veh-new-id')),
      updateVehicle: vi.fn().mockReturnValue(of(undefined)),
      updateVehicleStatus: vi.fn().mockReturnValue(of(undefined)),
      recordService: vi.fn().mockReturnValue(of(undefined)),
      assignVehicle: vi.fn().mockReturnValue(of('asg-id')),
      releaseAssignment: vi.fn().mockReturnValue(of(undefined)),
      getActiveAssignment: vi.fn().mockReturnValue(of(mockDetail.activeAssignment)),
      getVehicleAssignments: vi.fn().mockReturnValue(of([])),
      recordOdometerReading: vi.fn().mockReturnValue(of('odo-id')),
      getOdometerReadings: vi.fn().mockReturnValue(of([])),
      getDrivers: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
    };

    authServiceMock = {
      isAdminOrDispatcher: vi.fn().mockReturnValue(true),
      isAdmin: vi.fn().mockReturnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [VehiclesComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        I18nService,
        { provide: VehiclesService, useValue: vehiclesServiceMock },
        { provide: AuthService, useValue: authServiceMock },
      ],
    }).compileComponents();

    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', {
      COMMON: {
        UNITS: {
          KM: 'km',
          SLASH_KM: '/km',
        },
      },
    });

    fixture = TestBed.createComponent(VehiclesComponent);
    component = fixture.componentInstance;
  });

  it('should render vehicles list with rego and maintenance status in success state', () => {
    fixture.detectChanges();

    expect(component.state()).toBe('success');
    expect(component.vehicles().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.data-table')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
    expect(compiled.textContent).toContain('ABC123');
    expect(compiled.textContent).toContain('XYZ999');
    expect(compiled.textContent).toContain('32000 km');
  });

  it('should render empty state when no vehicles are found', () => {
    vehiclesServiceMock.getVehicles.mockReturnValue(
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

  it('should filter vehicles on search input and toggle serviceDueOnly', () => {
    fixture.detectChanges();

    component.onSearchInput({ target: { value: 'HiAce' } } as unknown as Event);
    expect(component.searchTerm()).toBe('HiAce');
    expect(vehiclesServiceMock.getVehicles).toHaveBeenCalledWith(
      expect.objectContaining({ search: 'HiAce' }),
    );

    component.onServiceDueToggle({ target: { checked: true } } as unknown as Event);
    expect(component.serviceDueOnly()).toBe(true);
    expect(vehiclesServiceMock.getVehicles).toHaveBeenCalledWith(
      expect.objectContaining({ serviceDueOnly: true }),
    );
  });

  it('should render error state with retry button on failure', () => {
    const errorResponse = new HttpErrorResponse({
      status: 500,
      error: { message: 'Failed to retrieve vehicles' },
    });
    vehiclesServiceMock.getVehicles.mockReturnValue(throwError(() => errorResponse));

    fixture.detectChanges();

    expect(component.state()).toBe('error');
    const compiled = fixture.nativeElement as HTMLElement;
    const retryBtn = compiled.querySelector('.state-card button') as HTMLButtonElement;
    expect(retryBtn).toBeTruthy();

    vehiclesServiceMock.getVehicles.mockReturnValue(of(mockPaginatedResult));
    retryBtn.click();
    expect(vehiclesServiceMock.getVehicles).toHaveBeenCalledTimes(2);
  });

  it('should validate and submit create vehicle form', () => {
    fixture.detectChanges();

    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.createForm.rego = 'NEW888';
    component.createForm.make = 'Isuzu';
    component.createForm.model = 'N-Series';
    component.createForm.year = 2024;
    component.createForm.vinEnc = 'VIN1234567890ABCD';
    component.createForm.odometerKm = 500;
    component.createForm.serviceIntervalKm = 15000;

    component.submitCreateVehicle();

    expect(vehiclesServiceMock.createVehicle).toHaveBeenCalledWith(
      expect.objectContaining({
        rego: 'NEW888',
        make: 'Isuzu',
        model: 'N-Series',
      }),
    );
    expect(component.isCreateModalOpen()).toBe(false);
  });

  it('should display duplicate rego error message on 409 conflict', () => {
    fixture.detectChanges();

    component.openCreateModal();
    component.createForm.rego = 'ABC123';
    component.createForm.make = 'Toyota';
    component.createForm.model = 'HiAce';
    component.createForm.vinEnc = 'VIN1234567890ABCD';

    const conflictError = new HttpErrorResponse({
      status: 409,
      statusText: 'Conflict',
      error: { message: 'Rego already exists' },
    });
    vehiclesServiceMock.createVehicle.mockReturnValue(throwError(() => conflictError));

    component.submitCreateVehicle();

    expect(component.formError()).toContain('already exists');
    expect(component.isCreateModalOpen()).toBe(true);
  });
});
