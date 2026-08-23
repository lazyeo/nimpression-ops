using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Compliance;

/// <summary>
/// 交通罚单聚合根。严格遵守状态流转 Submitted -> UnderReview -> {Accepted, Disputed, Waived}。
/// </summary>
public sealed class Fine : AggregateRoot
{
    public Guid DriverId { get; private set; }
    public Guid VehicleId { get; private set; }
    public DateOnly IssuedOn { get; private set; }
    public string Authority { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public Money Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? TicketPhotoKey { get; private set; }
    public FineStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }

    private Fine()
    {
    }

    public Fine(
        Guid id,
        Guid driverId,
        Guid vehicleId,
        DateOnly issuedOn,
        string authority,
        string reference,
        Money amount,
        string reason,
        string? ticketPhotoKey = null) : base(id)
    {
        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new DomainValidationException("Authority cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new DomainValidationException("Reference cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Reason cannot be empty.");
        }

        DriverId = driverId;
        VehicleId = vehicleId;
        IssuedOn = issuedOn;
        Authority = authority.Trim();
        Reference = reference.Trim().ToUpperInvariant();
        Amount = amount;
        Reason = reason.Trim();
        TicketPhotoKey = string.IsNullOrWhiteSpace(ticketPhotoKey) ? null : ticketPhotoKey.Trim();
        Status = FineStatus.Submitted;
    }

    public void StartReview(Guid reviewerUserId, DateTimeOffset reviewedAt)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainValidationException("Reviewer UserId cannot be empty.");
        }

        if (Status != FineStatus.Submitted)
        {
            throw new InvalidFineTransitionException(Status, FineStatus.UnderReview);
        }

        Status = FineStatus.UnderReview;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
    }

    public void Accept(Guid reviewerUserId, DateTimeOffset reviewedAt, string? reviewNote = null)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainValidationException("Reviewer UserId cannot be empty.");
        }

        if (Status != FineStatus.UnderReview)
        {
            throw new InvalidFineTransitionException(Status, FineStatus.Accepted);
        }

        Status = FineStatus.Accepted;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();

        AddDomainEvent(new FineAccepted(Id, DriverId, VehicleId, Amount, reviewedAt));
    }

    public void Dispute(Guid reviewerUserId, DateTimeOffset reviewedAt, string reviewNote)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainValidationException("Reviewer UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(reviewNote))
        {
            throw new DomainValidationException("Dispute review note is mandatory.");
        }

        if (Status != FineStatus.UnderReview)
        {
            throw new InvalidFineTransitionException(Status, FineStatus.Disputed);
        }

        Status = FineStatus.Disputed;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        ReviewNote = reviewNote.Trim();
    }

    public void Waive(Guid reviewerUserId, DateTimeOffset reviewedAt, string reviewNote)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainValidationException("Reviewer UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(reviewNote))
        {
            throw new DomainValidationException("Waive review note is mandatory.");
        }

        if (Status != FineStatus.UnderReview)
        {
            throw new InvalidFineTransitionException(Status, FineStatus.Waived);
        }

        Status = FineStatus.Waived;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        ReviewNote = reviewNote.Trim();
    }
}
