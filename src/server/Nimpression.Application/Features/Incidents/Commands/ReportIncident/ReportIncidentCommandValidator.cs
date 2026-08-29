using FluentValidation;

namespace Nimpression.Application.Features.Incidents.Commands.ReportIncident;

public sealed class ReportIncidentCommandValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithMessage("VehicleId is mandatory.");

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Location cannot be empty or exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description cannot be empty.");

        RuleFor(x => x.Severity)
            .IsInEnum()
            .WithMessage("Severity must be a valid IncidentSeverity value.");
    }
}
