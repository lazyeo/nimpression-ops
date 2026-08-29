using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Fines.Commands.DisputeFine;

public sealed class DisputeFineCommandHandler(
    IFineRepository fineRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<DisputeFineCommand, Result>
{
    public async Task<Result> Handle(DisputeFineCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Dispatcher))
        {
            return Error.Forbidden("forbidden", "Only administrators or dispatchers can dispute fines.");
        }

        if (!currentUser.UserId.HasValue)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        if (string.IsNullOrWhiteSpace(request.ReviewNote))
        {
            return Error.Validation("review_note_required", "Review note is mandatory when disputing a fine.");
        }

        var fine = await fineRepository.GetByIdAsync(request.FineId, cancellationToken);
        if (fine is null)
        {
            return Error.NotFound("fine_not_found", $"Fine with ID '{request.FineId}' was not found.");
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        try
        {
            fine.Dispute(currentUser.UserId.Value, now, request.ReviewNote);
        }
        catch (InvalidFineTransitionException ex)
        {
            return Error.Unprocessable("invalid_fine_transition", ex.Message);
        }
        catch (DomainValidationException ex)
        {
            return Error.Unprocessable("validation_error", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
