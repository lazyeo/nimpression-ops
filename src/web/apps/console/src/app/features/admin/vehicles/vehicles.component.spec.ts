import zhDictionary from '../../../../assets/i18n/zh-CN.json';
import enDictionary from '../../../../assets/i18n/en-NZ.json';
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
import { RealtimeService } from '../../../core/realtime/realtime.service';
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

    TestBed.inject(I18nService).setDictionary('en-NZ', enDictionary);
    TestBed.inject(I18nService).setDictionary('zh-CN', zhDictionary);
    TestBed.inject(I18nService).currentLang.set('en-NZ');

    fixture = TestBed.createComponent(VehiclesComponent);
    component = fixture.componentInstance;
  });

  it.each([
    ['en-NZ', 400, 'Some information is missing or invalid. Check your entries and try again. (OPS-400)'],
    ['en-NZ', 500, 'The service is temporarily unavailable. Try again later. (OPS-SERVICE)'],
    ['zh-CN', 400, '部分信息缺失或不正确，请检查填写内容后重试。 (OPS-400)'],
    ['zh-CN', 500, '服务暂时不可用，请稍后再试。 (OPS-SERVICE)'],
  ] as const)('renders a safe %s error for status %s', (language, status, expected) => {
    TestBed.inject(I18nService).currentLang.set(language);
    vehiclesServiceMock.getVehicles.mockReturnValue(throwError(() => new HttpErrorResponse({
      status,
      url: 'https://internal.invalid/api/vehicles',
      error: { detail: 'SQL connection failed at https://internal.invalid/api/vehicles', message: 'private stack trace' },
    })));
    fixture.detectChanges();

    expect(component.errorMessage()).toBe(expected);
    const message = (fixture.nativeElement as HTMLElement).querySelector('.state-desc')?.textContent;
    expect(message).toContain(expected);
    expect(message).not.toMatch(/internal\.invalid|SQL|private stack|ERRORS\./);
  });

  it.each([
    ['en-NZ', 'Driver Assignment'],
    ['zh-CN', '司机分配'],
  ] as const)('renders the assignment heading in %s', (language, heading) => {
    TestBed.inject(I18nService).currentLang.set(language);
    fixture.detectChanges();
    component.openDetailsModal(mockVehicles[0]);
    fixture.detectChanges();
    const content = (fixture.nativeElement as HTMLElement).textContent;
    expect(content).toContain(heading);
    expect(content).not.toContain('VEHICLES.SECTION_ASSIGNMENT');
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
      error: { title: 'vehicle_rego_conflict', detail: 'database index violated at https://internal.invalid/api/vehicles' },
    });
    vehiclesServiceMock.createVehicle.mockReturnValue(throwError(() => conflictError));

    component.submitCreateVehicle();

    expect(component.formError()).toBe('This registration plate is already in use. Check the plate or the existing vehicle. (VEHICLE-002)');
    expect(component.formError()).not.toContain('internal.invalid');
    expect(component.isCreateModalOpen()).toBe(true);
  });

  it('should automatically reload vehicles when SignalR invalidation signal arrives for vehicle entity', () => {
    fixture.detectChanges();
    expect(vehiclesServiceMock.getVehicles).toHaveBeenCalledTimes(1);

    const realtime = TestBed.inject(RealtimeService);
    (realtime as any).invalidationSubject.next({
      kind: 'vehicle.service_threshold_reached',
      entityId: 'veh-1',
      occurredAt: new Date().toISOString(),
    });

    expect(vehiclesServiceMock.getVehicles).toHaveBeenCalledTimes(2);
  });

  it('should count vehicles with expired or expiring compliance in alert count even if isServiceDue is false', () => {
    const expiredVehicles: VehicleSummaryDto[] = [
      {
        id: 'veh-exp-1',
        rego: 'NIM005',
        make: 'Isuzu',
        model: 'Forward',
        year: 2021,
        odometerKm: 50000,
        serviceIntervalKm: 15000,
        lastServiceOdometerKm: 48000,
        distanceSinceLastServiceKm: 2000,
        isServiceDue: false,
        wofExpiry: '2026-08-30', // expired
        status: 'Active',
      },
      {
        id: 'veh-exp-2',
        rego: 'NIM006',
        make: 'Hino',
        model: '500',
        year: 2020,
        odometerKm: 80000,
        serviceIntervalKm: 20000,
        lastServiceOdometerKm: 75000,
        distanceSinceLastServiceKm: 5000,
        isServiceDue: false,
        insuranceExpiry: '2026-09-06', // expired
        status: 'Active',
      },
      {
        id: 'veh-ok-1',
        rego: 'NIM099',
        make: 'Scania',
        model: 'R500',
        year: 2024,
        odometerKm: 10000,
        serviceIntervalKm: 30000,
        lastServiceOdometerKm: 10000,
        distanceSinceLastServiceKm: 0,
        isServiceDue: false,
        wofExpiry: '2027-12-31', // far future
        cofExpiry: '2027-12-31',
        insuranceExpiry: '2027-12-31',
        status: 'Active',
      },
    ];

    component.vehicles.set(expiredVehicles);
    expect(component.serviceDueCount()).toBe(2);
  });
});
