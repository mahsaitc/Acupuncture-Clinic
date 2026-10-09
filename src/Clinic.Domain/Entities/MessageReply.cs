namespace Clinic.Domain.Entities;

/// <summary>A reply in a message's conversation: from the clinic (a doctor, the admin or reception) or from the patient.</summary>
public class MessageReply
{
    public int Id { get; set; }
    public int ContactMessageId { get; set; }

    /// <summary>Who wrote it.</summary>
    public string AuthorUserId { get; set; } = default!;

    /// <summary>True when the clinic wrote it, false when the patient answered back.</summary>
    public bool FromClinic { get; set; }

    public string Body { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? EditedUtc { get; set; }
}
