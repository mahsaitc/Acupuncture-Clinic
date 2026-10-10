using Clinic.Domain.Entities;

namespace Clinic.Web.Clinical;

/// <summary>The model of the _BodyMap partial: every chart page with a session's points.</summary>
public sealed record BodyMap(IReadOnlyList<SessionPoint> Points, bool Editable);
