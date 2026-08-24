using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<Result<LoginResultDto>>;
