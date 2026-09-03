import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { IncidentsComponent } from './incidents.component';
import { IncidentsService } from './services/incidents.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';
import { IncidentReportDto, IncidentReportDetailDto } from './models/incidents.models';

describe('IncidentsComponent', () => {
  let component: IncidentsComponent;
  let fixture: ComponentFixture<IncidentsComponent>;
  let incidentsService: any;

  const mockIncidents: IncidentReportDto[] = [
    {
      id: 'inc-1',
      driverId: 'driver-1',
      driverName: 'Charlie Davis',
      employeeNo: 'DRV005',
      vehicleId: 'veh-1',
      vehicleRego: 'NIM-01',
      occurredAt: '2026-09-02T14:30:00Z',
      location: 'State Highway 1, Penrose',
      severity: 'Moderate',
      description: 'Side mirror damaged by merging truck',
      thirdPartyInfo: 'NZ Courier Van, Rego: EXP-112',
      status: 'Open',
      insurerNotifiedAt: '2026-09-02T14:35:00Z',
      photoKeys: ['incidents/mirror.jpg'],
      notifiedInsurer: true,
    },
    {
      id: 'inc-2',
      driverId: 'driver-2',
      driverName: 'David Evans',
      employeeNo: 'DRV006',
      vehicleId: 'veh-2',
      vehicleRego: 'NIM-02',
      occurredAt: '2026-09-01T09:15:00Z',
      location: 'Queen Street Depot',
      severity: 'Minor',
      description: 'Minor bumper scratch during reverse parking',
      thirdPartyInfo: null,
      status: 'Closed',
      insurerNotifiedAt: null,
      photoKeys: [],
      notifiedInsurer: false,
    },
  ];

  const mockDetail: IncidentReportDetailDto = {
    ...mockIncidents[0],
    photoUrls: ['https://storage.nimpression.nz/signed-mirror.jpg?token=xyz'],
  };

  beforeEach(async () => {
    incidentsService = {
      getIncidents: vi.fn().mockReturnValue(
        of({
          items: mockIncidents,
          totalCount: 2,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      ),
      getIncidentById: vi.fn().mockReturnValue(of(mockDetail)),
      reportIncident: vi.fn().mockReturnValue(of(mockIncidents[0])),
      getDrivers: vi.fn().mockReturnValue(of({ items: [] })),
      getVehicles: vi.fn().mockReturnValue(of({ items: [] })),
    };

    await TestBed.configureTestingModule({
      imports: [IncidentsComponent],
      providers: [
        I18nService,
        FormatService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IncidentsService, useValue: incidentsService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(IncidentsComponent);
    component = fixture.componentInstance;
  });

  it('renders incident list with locations and severity badges (List rendering test)', () => {
    fixture.detectChanges();

    expect(component.incidents().length).toBe(2);
    expect(component.isLoading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('State Highway 1, Penrose');
    expect(compiled.textContent).toContain('Queen Street Depot');
  });

  it('renders empty data state when no incidents match (Empty state test)', () => {
    incidentsService.getIncidents.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
    );

    component.loadIncidents();
    fixture.detectChanges();

    expect(component.incidents().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('filters incidents by severity and search term (Filter test)', () => {
    component.selectedSeverity.set('Moderate');
    component.searchTerm.set('Penrose');

    component.applyFilters();
    fixture.detectChanges();

    expect(incidentsService.getIncidents).toHaveBeenCalledWith(
      expect.objectContaining({
        severity: 'Moderate',
        searchTerm: 'Penrose',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('handles error state properly when API fails (Error state test)', () => {
    incidentsService.getIncidents.mockReturnValue(
      throwError(() => ({ status: 500, message: 'Server error' })),
    );

    component.loadIncidents();
    fixture.detectChanges();

    expect(component.hasError()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();
  });

  it('opens report incident dialog and validates mandatory inputs (Validation test)', () => {
    component.openReportModal();
    expect(component.isReportModalOpen()).toBe(true);

    component.submitReport();
    expect(component.reportError()).toBe('Please fill in all mandatory fields.');
    expect(incidentsService.reportIncident).not.toHaveBeenCalled();

    component.newVehicleId = 'veh-1';
    component.newOccurredAt = '2026-09-03T10:00';
    component.newLocation = 'Hobson St';
    component.newSeverity = 'Moderate';
    component.newDescription = 'Rear bumper scratch';

    component.submitReport();
    expect(incidentsService.reportIncident).toHaveBeenCalled();
  });

  it('shows insurer notification card and photo gallery in incident detail modal (Detail test)', () => {
    component.viewDetails(mockIncidents[0]);
    fixture.detectChanges();

    expect(component.isDetailModalOpen()).toBe(true);
    expect(component.activeIncidentDetail()?.notifiedInsurer).toBe(true);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.insurer-notice-card')).toBeTruthy();
    expect(compiled.querySelectorAll('.gallery-item').length).toBe(1);
  });
});
