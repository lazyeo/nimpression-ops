import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { DriversComponent } from './drivers.component';
import { DriversService } from './services/drivers.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { DriverDetailDto, DriverSummaryDto, PaginatedResult } from './models/drivers.models';

describe('DriversComponent', () => {
  let component: DriversComponent;
  let fixture: ComponentFixture<DriversComponent>;
  let driversServiceMock: {
    getDrivers: ReturnType<typeof vi.fn>;
    getLicenceAlerts: ReturnType<typeof vi.fn>;
    getDriverById: ReturnType<typeof vi.fn>;
    createDriver: ReturnType<typeof vi.fn>;
    updateDriver: ReturnType<typeof vi.fn>;
    deactivateDriver: ReturnType<typeof vi.fn>;
    uploadAvatar: ReturnType<typeof vi.fn>;
    getAreas: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    isAdminOrDispatcher: ReturnType<typeof vi.fn>;
    isAdmin: ReturnType<typeof vi.fn>;
  };

  const mockDrivers: DriverSummaryDto[] = [
    {
      id: 'drv-1',
      userId: 'usr-1',
      employeeNo: 'DRV-1001',
      displayName: 'Alex Mercer',
      email: 'alex.m@example.co.nz',
      licenceClass: 'Class 2 Heavy Rigid',
      licenceExpiry: '2026-12-31',
      isLicenceExpiringSoon: false,
      isLicenceExpired: false,
      daysUntilLicenceExpiry: 120,
      status: 'Active',
      hiredOn: '2025-01-15',
      hourlyRate: 34.5,
      perTripRate: 50.0,
      perKmRate: 1.2,
      assignedAreaNames: ['Auckland Central'],
      activeAreaIds: ['area-1'],
    },
    {
      id: 'drv-2',
      userId: 'usr-2',
      employeeNo: 'DRV-1002',
      displayName: 'Sarah Connor',
      email: 'sarah.c@example.co.nz',
      licenceClass: 'Class 4 Heavy Combination',
      licenceExpiry: '2026-09-15',
      isLicenceExpiringSoon: true,
      isLicenceExpired: false,
      daysUntilLicenceExpiry: 12,
      status: 'Active',
      hiredOn: '2024-06-01',
      hourlyRate: 36.0,
      perTripRate: 18.0,
      perKmRate: 0.85,
      assignedAreaNames: ['Manukau Metro'],
      activeAreaIds: ['area-2'],
    },
  ];

  const mockDetail: DriverDetailDto = {
    ...mockDrivers[0],
    hourlyRateAmount: 34.5,
    hourlyRateCurrency: 'NZD',
    perTripRateAmount: 50.0,
    perTripRateCurrency: 'NZD',
    perKmRateAmount: 1.2,
    perKmRateCurrency: 'NZD',
    phone: '+64 21 000 1111',
    address: '10 Queen St, Auckland',
    emergencyContact: 'Emma - 021 999 8888',
    locale: 'en-NZ',
    areaAssignments: [
      {
        id: 'asg-1',
        areaId: 'area-1',
        areaName: 'Auckland Central',
        areaCode: 'AKL-CBD',
        driverId: 'drv-1',
        effectiveFrom: '2025-01-15',
        isActive: true,
      },
    ],
  };

  const mockPaginatedResult: PaginatedResult<DriverSummaryDto> = {
    items: mockDrivers,
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    driversServiceMock = {
      getDrivers: vi.fn().mockReturnValue(of(mockPaginatedResult)),
      getLicenceAlerts: vi.fn().mockReturnValue(of([])),
      getDriverById: vi.fn().mockReturnValue(of(mockDetail)),
      createDriver: vi.fn().mockReturnValue(of(mockDetail)),
      updateDriver: vi.fn().mockReturnValue(of(mockDetail)),
      deactivateDriver: vi.fn().mockReturnValue(of(undefined)),
      uploadAvatar: vi.fn().mockReturnValue(of({ avatarKey: 'key', avatarUrl: 'url' })),
      getAreas: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
    };

    authServiceMock = {
      isAdminOrDispatcher: vi.fn().mockReturnValue(true),
      isAdmin: vi.fn().mockReturnValue(true),
    };

    await TestBed.configureTestingModule({
      imports: [DriversComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        I18nService,
        { provide: DriversService, useValue: driversServiceMock },
        { provide: AuthService, useValue: authServiceMock },
      ],
    }).compileComponents();

    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', {
      COMMON: {
        UNITS: {
          PER_HR: '/hr',
          PER_TRIP: '/trip',
          PER_KM: '/km',
        },
      },
    });

    fixture = TestBed.createComponent(DriversComponent);
    component = fixture.componentInstance;
  });

  it('should render drivers list with compliance and rate cards in success state', () => {
    fixture.detectChanges();

    expect(component.state()).toBe('success');
    expect(component.drivers().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.data-table')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
    expect(compiled.textContent).toContain('Alex Mercer');
    expect(compiled.textContent).toContain('DRV-1001');
    expect(compiled.textContent).toContain('$34.5/hr');
    expect(compiled.textContent).toContain('$50/trip | $1.2/km');
  });

  it('should render empty state when drivers list is empty', () => {
    driversServiceMock.getDrivers.mockReturnValue(
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

  it('should filter drivers on search input and reset page to 1', () => {
    fixture.detectChanges();

    component.onSearchInput({ target: { value: 'Mercer' } } as unknown as Event);

    expect(component.searchTerm()).toBe('Mercer');
    expect(component.currentPage()).toBe(1);
    expect(driversServiceMock.getDrivers).toHaveBeenCalledWith(
      expect.objectContaining({ searchTerm: 'Mercer' }),
    );
  });

  it('should render error state with retry button on failure', () => {
    const errorResponse = new HttpErrorResponse({
      status: 500,
      error: { message: 'Server unavailable' },
    });
    driversServiceMock.getDrivers.mockReturnValue(throwError(() => errorResponse));

    fixture.detectChanges();

    expect(component.state()).toBe('error');
    const compiled = fixture.nativeElement as HTMLElement;
    const retryBtn = compiled.querySelector('.state-card button') as HTMLButtonElement;
    expect(retryBtn).toBeTruthy();

    driversServiceMock.getDrivers.mockReturnValue(of(mockPaginatedResult));
    retryBtn.click();
    expect(driversServiceMock.getDrivers).toHaveBeenCalledTimes(2);
  });

  it('should validate and create driver with Admin role', () => {
    fixture.detectChanges();

    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.createForm.displayName = 'New Driver';
    component.createForm.email = 'new.driver@example.co.nz';
    component.createForm.employeeNo = 'DRV-9999';
    component.createForm.licenceClass = 'Class 2';
    component.createForm.licenceExpiry = '2027-01-01';
    component.createForm.phone = '+64 21 555 4444';
    component.createForm.address = 'Auckland';
    component.createForm.emergencyContact = 'Contact';

    component.submitCreateDriver();

    expect(driversServiceMock.createDriver).toHaveBeenCalledWith(
      expect.objectContaining({
        displayName: 'New Driver',
        employeeNo: 'DRV-9999',
        licenceClass: 'Class 2',
      }),
    );
    expect(component.isCreateModalOpen()).toBe(false);
  });

  it('should display error when avatar upload returns 415 magic-byte mismatch', () => {
    fixture.detectChanges();

    component.openAvatarModal(mockDrivers[0]);
    expect(component.isAvatarModalOpen()).toBe(true);

    const fakeFile = new File(['fake image content'], 'avatar.jpg', { type: 'image/jpeg' });
    component.selectedAvatarFile = fakeFile;

    const error415 = new HttpErrorResponse({
      status: 415,
      statusText: 'Unsupported Media Type',
      error: { error: 'invalid_magic_bytes', message: 'Magic byte check failed' },
    });
    driversServiceMock.uploadAvatar.mockReturnValue(throwError(() => error415));

    component.submitAvatarUpload();

    expect(component.formError()).toContain('415');
    expect(component.isAvatarModalOpen()).toBe(true);
  });
});
