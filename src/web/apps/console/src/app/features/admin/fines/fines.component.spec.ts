import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { FinesComponent } from './fines.component';
import { FinesService } from './services/fines.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';
import { FineDto, FineDetailDto } from './models/fines.models';

describe('FinesComponent', () => {
  let component: FinesComponent;
  let fixture: ComponentFixture<FinesComponent>;
  let finesService: any;

  const mockFines: FineDto[] = [
    {
      id: 'fine-1',
      driverId: 'driver-1',
      driverName: 'Bob Brown',
      employeeNo: 'DRV003',
      vehicleId: 'veh-1',
      vehicleRego: 'NIM-01',
      issuedOn: '2026-09-02',
      authority: 'NZTA',
      reference: 'NZTA-9901',
      amount: 120.0,
      currency: 'NZD',
      reason: 'Bus lane violation',
      status: 'Submitted',
      ticketPhotoKey: 'fines/sample-photo.jpg',
    },
    {
      id: 'fine-2',
      driverId: 'driver-2',
      driverName: 'Alice Green',
      employeeNo: 'DRV004',
      vehicleId: 'veh-2',
      vehicleRego: 'NIM-02',
      issuedOn: '2026-09-01',
      authority: 'Auckland Transport',
      reference: 'AT-8812',
      amount: 150.0,
      currency: 'NZD',
      reason: 'Speeding 12km/h over limit',
      status: 'UnderReview',
    },
  ];

  const mockDetail: FineDetailDto = {
    ...mockFines[0],
    ticketPhotoUrl: 'https://storage.nimpression.nz/signed-url-for-photo.jpg?signature=abc',
    reviewedByUserId: null,
    reviewerName: null,
    reviewedAt: null,
    reviewNote: null,
  };

  beforeEach(async () => {
    finesService = {
      getFines: vi.fn().mockReturnValue(
        of({
          items: mockFines,
          totalCount: 2,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      ),
      getFineById: vi.fn().mockReturnValue(of(mockDetail)),
      getFinePhotoUrl: vi.fn().mockReturnValue(
        of({ url: 'https://storage.nimpression.nz/signed-url.jpg' }),
      ),
      submitFine: vi.fn().mockReturnValue(of(mockFines[0])),
      startReview: vi.fn().mockReturnValue(of(void 0)),
      acceptFine: vi.fn().mockReturnValue(of(void 0)),
      disputeFine: vi.fn().mockReturnValue(of(void 0)),
      waiveFine: vi.fn().mockReturnValue(of(void 0)),
      getDrivers: vi.fn().mockReturnValue(of({ items: [] })),
      getVehicles: vi.fn().mockReturnValue(of({ items: [] })),
    };

    await TestBed.configureTestingModule({
      imports: [FinesComponent],
      providers: [
        I18nService,
        FormatService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: FinesService, useValue: finesService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FinesComponent);
    component = fixture.componentInstance;
  });

  it('renders fine list and displays reference numbers (List rendering test)', () => {
    fixture.detectChanges();

    expect(component.fines().length).toBe(2);
    expect(component.isLoading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('NZTA-9901');
    expect(compiled.textContent).toContain('AT-8812');
  });

  it('renders empty data state when no fines exist (Empty state test)', () => {
    finesService.getFines.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
    );

    component.loadFines();
    fixture.detectChanges();

    expect(component.fines().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('applies filters with driver, status, and search term (Filter test)', () => {
    component.selectedStatus.set('Submitted');
    component.searchTerm.set('NZTA');

    component.applyFilters();
    fixture.detectChanges();

    expect(finesService.getFines).toHaveBeenCalledWith(
      expect.objectContaining({
        status: 'Submitted',
        searchTerm: 'NZTA',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('handles error state properly when API call fails (Error state test)', () => {
    finesService.getFines.mockReturnValue(
      throwError(() => ({ status: 500, message: 'Server error' })),
    );

    component.loadFines();
    fixture.detectChanges();

    expect(component.hasError()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();
  });

  it('opens submit modal and enforces required fields (Validation test)', () => {
    component.openSubmitModal();
    expect(component.isSubmitModalOpen()).toBe(true);

    // Empty submission should trigger validation error
    component.submitFine();
    expect(component.submitError()).toBe('Please fill in all mandatory fields.');
    expect(finesService.submitFine).not.toHaveBeenCalled();

    // Fill valid data
    component.newVehicleId = 'veh-1';
    component.newIssuedOn = '2026-09-03';
    component.newAuthority = 'NZTA';
    component.newReference = 'TKT-1234';
    component.newAmount = 150;
    component.newReason = 'Speeding';

    component.submitFine();
    expect(finesService.submitFine).toHaveBeenCalled();
  });

  it('displays presigned photo URL in detail modal without URL concatenation (Signed URL photo test)', () => {
    component.viewDetails(mockFines[0]);
    fixture.detectChanges();

    expect(component.isDetailModalOpen()).toBe(true);
    expect(component.activeFineDetail()?.ticketPhotoUrl).toBe(
      'https://storage.nimpression.nz/signed-url-for-photo.jpg?signature=abc',
    );

    const compiled = fixture.nativeElement as HTMLElement;
    const img = compiled.querySelector('.ticket-img') as HTMLImageElement;
    expect(img).toBeTruthy();
    expect(img.src).toContain('signed-url-for-photo.jpg');
  });

  it('handles review transitions and requires justification for dispute/waive (Review flow test)', () => {
    component.openReviewModal(mockFines[1], 'dispute');
    expect(component.isReviewModalOpen()).toBe(true);
    expect(component.reviewMode()).toBe('dispute');

    // Empty note for dispute
    component.reviewNote.set('  ');
    component.submitReviewDecision();
    expect(component.reviewError()).toBe('Review note / explanation is mandatory.');
    expect(finesService.disputeFine).not.toHaveBeenCalled();

    // With note
    component.reviewNote.set('Camera calibration certificate expired');
    component.submitReviewDecision();
    expect(finesService.disputeFine).toHaveBeenCalledWith(
      'fine-2',
      expect.objectContaining({ reviewNote: 'Camera calibration certificate expired' }),
    );
  });
});
