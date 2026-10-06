using Clinic.Domain.Entities;

namespace Clinic.Web.Clinical;

/// <summary>The model of the _BodyMap partial: the front and back diagrams with a session's points.</summary>
public sealed record BodyMap(IReadOnlyList<SessionPoint> Points, bool Editable);
