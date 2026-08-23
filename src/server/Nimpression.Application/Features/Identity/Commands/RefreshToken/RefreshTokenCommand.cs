using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Identity.DTOs;

namespace Nimpression.Application.Features.Identity.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string? RawRefreshToken,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<Result<LoginResultDto>>;
