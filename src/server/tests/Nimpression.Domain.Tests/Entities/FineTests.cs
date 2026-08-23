using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class FineTests
{
    private static Fine CreateFineInState(FineStatus state)
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var issuedOn = new DateOnly(2026, 8, 1);
        var fine = new Fine(
            Guid.NewGuid(),
            driverId,
            vehicleId,
            issuedOn,
            "NZ Police",
            "REF-FINE-001",
            new Money(150m),
            "Speeding 65 in 50 zone");

        if (state == FineStatus.Submitted)
        {
            return fine;
        }

        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        fine.StartReview(reviewerId, now);

        if (state == FineStatus.UnderReview)
        {
            return fine;
        }

        if (state == FineStatus.Accepted)
        {
            fine.Accept(reviewerId, now.AddHours(1), "Accepted by driver");
            return fine;
        }

        if (state == FineStatus.Disputed)
        {
            fine.Dispute(reviewerId, now.AddHours(1), "Incorrect speed camera");
            return fine;
        }

        if (state == FineStatus.Waived)
        {
            fine.Waive(reviewerId, now.AddHours(1), "Waived due to vehicle sale");
            return fine;
        }

        throw new ArgumentOutOfRangeException(nameof(state), state, null);
    }

    [Fact]
    public void Fine_happy_path_accept_emits_event()
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var fine = new Fine(
            Guid.NewGuid(),
            driverId,
            vehicleId,
            new DateOnly(2026, 8, 10),
            "Auckland Transport",
            "AT-12345",
            new Money(80m),
            "Bus lane violation",
            "photo_key.jpg");

        Assert.Equal(FineStatus.Submitted, fine.Status);
        Assert.Equal("photo_key.jpg", fine.TicketPhotoKey);

        var reviewTime = DateTimeOffset.UtcNow;
        fine.StartReview(reviewerId, reviewTime);
        Assert.Equal(FineStatus.UnderReview, fine.Status);
        Assert.Equal(reviewerId, fine.ReviewedByUserId);
        Assert.Equal(reviewTime, fine.ReviewedAt);

        var acceptTime = reviewTime.AddHours(2);
        fine.Accept(reviewerId, acceptTime, "Agreed");
        Assert.Equal(FineStatus.Accepted, fine.Status);
        Assert.Equal(acceptTime, fine.ReviewedAt);
        Assert.Equal("Agreed", fine.ReviewNote);

        var domainEvent = Assert.IsType<FineAccepted>(Assert.Single(fine.DomainEvents));
        Assert.Equal(fine.Id, domainEvent.FineId);
        Assert.Equal(driverId, domainEvent.DriverId);
        Assert.Equal(vehicleId, domainEvent.VehicleId);
        Assert.Equal(new Money(80m), domainEvent.Amount);
    }

    [Theory]
    [InlineData(FineStatus.Disputed)]
    [InlineData(FineStatus.Waived)]
    public void Fine_dispute_and_waive_terminal_flows(FineStatus targetStatus)
    {
        var fine = CreateFineInState(FineStatus.UnderReview);
        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        if (targetStatus == FineStatus.Disputed)
        {
            fine.Dispute(reviewerId, now, "Dispute note");
            Assert.Equal(FineStatus.Disputed, fine.Status);
        }
        else
        {
            fine.Waive(reviewerId, now, "Waive note");
            Assert.Equal(FineStatus.Waived, fine.Status);
        }

        Assert.Empty(fine.DomainEvents);
    }

    [Theory]
    // From Submitted
    [InlineData(FineStatus.Submitted, FineStatus.Accepted)]
    [InlineData(FineStatus.Submitted, FineStatus.Disputed)]
    [InlineData(FineStatus.Submitted, FineStatus.Waived)]
    // From UnderReview
    [InlineData(FineStatus.UnderReview, FineStatus.Submitted)]
    // From Accepted (Terminal)
    [InlineData(FineStatus.Accepted, FineStatus.Submitted)]
    [InlineData(FineStatus.Accepted, FineStatus.UnderReview)]
    [InlineData(FineStatus.Accepted, FineStatus.Disputed)]
    [InlineData(FineStatus.Accepted, FineStatus.Waived)]
    // From Disputed (Terminal)
    [InlineData(FineStatus.Disputed, FineStatus.Submitted)]
    [InlineData(FineStatus.Disputed, FineStatus.UnderReview)]
    [InlineData(FineStatus.Disputed, FineStatus.Accepted)]
    [InlineData(FineStatus.Disputed, FineStatus.Waived)]
    // From Waived (Terminal)
    [InlineData(FineStatus.Waived, FineStatus.Submitted)]
    [InlineData(FineStatus.Waived, FineStatus.UnderReview)]
    [InlineData(FineStatus.Waived, FineStatus.Accepted)]
    [InlineData(FineStatus.Waived, FineStatus.Disputed)]
    public void Fine_invalid_transitions_throw_InvalidFineTransitionException(
        FineStatus fromState, FineStatus toState)
    {
        var fine = CreateFineInState(fromState);
        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<InvalidFineTransitionException>(() =>
        {
            switch (toState)
            {
                case FineStatus.UnderReview:
                    fine.StartReview(reviewerId, now);
                    break;
                case FineStatus.Accepted:
                    fine.Accept(reviewerId, now);
                    break;
                case FineStatus.Disputed:
                    fine.Dispute(reviewerId, now, "Note");
                    break;
                case FineStatus.Waived:
                    fine.Waive(reviewerId, now, "Note");
                    break;
                default:
                    throw new InvalidFineTransitionException(fromState, toState);
            }
        });

        Assert.Equal(fromState, ex.From);
        Assert.Equal(toState, ex.To);
    }
}
