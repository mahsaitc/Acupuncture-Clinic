using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Localization;

/// <summary>Translates the Identity errors that patients actually see at sign-up.</summary>
public class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> l) : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = l["An account with this email already exists."] };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = l["An account with this email already exists."] };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = l["The password must be at least {0} characters.", length] };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = l["The password must contain a digit."] };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = l["The password must contain a lowercase English letter."] };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = l["The password must contain an uppercase English letter."] };
}
