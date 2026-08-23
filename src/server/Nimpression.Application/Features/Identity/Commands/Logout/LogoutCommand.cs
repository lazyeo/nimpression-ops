using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Identity.Commands.Logout;

public sealed record LogoutCommand(string? RawRefreshToken) : IRequest<Result>, ICommandMarker;
