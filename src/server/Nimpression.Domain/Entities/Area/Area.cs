using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Area;

/// <summary>
/// 运营区域聚合根。包含区域编码、多边形地理边界与启用状态。
/// </summary>
public sealed class Area : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? GeoJson { get; private set; }
    public bool IsActive { get; private set; }

    private Area()
    {
    }

    public Area(
        Guid id,
        string name,
        string code,
        string? description = null,
        string? geoJson = null,
        bool isActive = true) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Area name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException("Area code cannot be empty.");
        }

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        GeoJson = string.IsNullOrWhiteSpace(geoJson) ? null : geoJson.Trim();
        IsActive = isActive;
    }

    public void UpdateDetails(string name, string code, string? description, string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Area name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException("Area code cannot be empty.");
        }

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        GeoJson = string.IsNullOrWhiteSpace(geoJson) ? null : geoJson.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
