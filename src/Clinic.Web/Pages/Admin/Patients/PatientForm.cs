using System.ComponentModel.DataAnnotations;
using Clinic.Application.Common;
using Clinic.Domain;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Identity;
using Clinic.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Clinic.Web.Pages.Admin.Patients;

/// <summary>The front-desk patient form: account details plus the non-clinical profile.</summary>
public class PatientInput
{
    [Required(ErrorMessage = "{0} is required.")]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "{0} is required.")]
    [RegularExpression(@"^\s*(09|۰۹)[0-9۰-۹]{9}\s*$|^\+\d{8,15}$", ErrorMessage = "Enter a mobile number like 09121234567.")]
    [Display(Name = "Mobile number")]
    public string Mobile { get; set; } = "";

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200)]
    [Display(Name = "Email (optional)")]
    public string? Email { get; set; }

    [RegularExpression("^[0-9۰-۹]{10}$", ErrorMessage = "The national code has 10 digits.")]
    [Display(Name = "National code")]
    public string? NationalCode { get; set; }

    [Display(Name = "Birth date")]
    public string? BirthDate { get; set; }

    [Display(Name = "Gender")]
    public Gender? Gender { get; set; }

    [Display(Name = "Marital status")]
    public MaritalStatus? MaritalStatus { get; set; }

    [Range(0, 30, ErrorMessage = "{0} must be between {1} and {2}.")]
    [Display(Name = "Number of children")]
    public int? Children { get; set; }

    [StringLength(20)]
    [Display(Name = "Date of first visit")]
    public string? FirstVisitDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Father's name")]
    public string? FatherName { get; set; }

    [StringLength(100)]
    [Display(Name = "Occupation")]
    public string? Occupation { get; set; }

    [StringLength(100)]
    [Display(Name = "Education")]
    public string? Education { get; set; }

    [StringLength(30)]
    [Display(Name = "Landline phone")]
    public string? LandlinePhone { get; set; }

    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }

    [StringLength(400)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    [StringLength(200)]
    [Display(Name = "Referred by")]
    public string? ReferralSource { get; set; }

    [StringLength(100)]
    [Display(Name = "Insurance")]
    public string? InsuranceProvider { get; set; }

    [StringLength(200)]
    [Display(Name = "Emergency contact")]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    [Display(Name = "Emergency contact phone")]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(2000)]
    [Display(Name = "Front desk notes")]
    public string? Notes { get; set; }

    public static PatientInput From(ApplicationUser user, PatientProfile? p) => new()
    {
        FullName = user.FullName,
        Mobile = user.PhoneNumber ?? "",
        Email = user.Email,
        NationalCode = p?.NationalCode,
        BirthDate = DisplayFormat.DateInput(p?.BirthDate),
        Gender = p?.Gender,
        MaritalStatus = p?.MaritalStatus,
        Children = p?.Children,
        FirstVisitDate = DisplayFormat.DateInput(p?.FirstVisitDate),
        FatherName = p?.FatherName,
        Occupation = p?.Occupation,
        Education = p?.Education,
        LandlinePhone = p?.LandlinePhone,
        City = p?.City,
        Address = p?.Address,
        PostalCode = p?.PostalCode,
        ReferralSource = p?.ReferralSource,
        InsuranceProvider = p?.InsuranceProvider,
        EmergencyContactName = p?.EmergencyContactName,
        EmergencyContactPhone = p?.EmergencyContactPhone,
        Notes = p?.Notes,
    };
}

/// <summary>Creates and updates patients from the management panel.</summary>
public class PatientRegistration(ClinicDbContext db, UserManager<ApplicationUser> users, TimeProvider time, IStringLocalizer<SharedResource> l)
{
    /// <summary>Normalises a mobile number to Latin digits without spaces.</summary>
    public static string NormalizeMobile(string? mobile) => JalaliDate.ToLatinDigits(mobile).Trim().Replace(" ", "");

    /// <summary>
    /// Checks what the attributes cannot: the dates, and that the mobile and email are not already in use.
    /// Returns the parsed dates.
    /// </summary>
    public async Task<PatientDates> ValidateAsync(PatientInput input, ModelStateDictionary modelState, string? existingUserId)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var birth = ParseDate(input.BirthDate, "Input.BirthDate", today.AddYears(-120), today, modelState);
        var firstVisit = ParseDate(input.FirstVisitDate, "Input.FirstVisitDate", today.AddYears(-50), today.AddDays(1), modelState);

        var mobile = NormalizeMobile(input.Mobile);
        if (mobile.Length > 0 && await db.Users.AnyAsync(u => u.PhoneNumber == mobile && u.Id != existingUserId))
        {
            modelState.AddModelError("Input.Mobile", l["A patient with this mobile number is already registered."]);
        }
        if (!string.IsNullOrWhiteSpace(input.Email))
        {
            var normalized = users.NormalizeEmail(input.Email.Trim());
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized && u.Id != existingUserId))
            {
                modelState.AddModelError("Input.Email", l["An account with this email already exists."]);
            }
        }
        return new PatientDates(birth, firstVisit);
    }

    private DateOnly? ParseDate(string? text, string key, DateOnly min, DateOnly max, ModelStateDictionary modelState)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (DisplayFormat.TryParseDateInput(text, out var parsed) && parsed >= min && parsed <= max)
        {
            return parsed;
        }
        modelState.AddModelError(key, l["Enter the date like {0}.", DisplayFormat.DateInputHint]);
        return null;
    }

    /// <summary>
    /// Creates a patient account without a password: the patient cannot sign in with it until the clinic
    /// gives them access, so nobody can claim the account just by knowing the mobile number.
    /// </summary>
    public async Task<ApplicationUser?> CreateAsync(PatientInput input, PatientDates dates, string staffUserId, ModelStateDictionary modelState)
    {
        var mobile = NormalizeMobile(input.Mobile);
        var email = Clean(input.Email);
        var user = new ApplicationUser
        {
            UserName = email ?? $"m{mobile.TrimStart('+')}",
            Email = email,
            PhoneNumber = mobile,
            FullName = input.FullName.Trim(),
            PreferredLanguage = CulturePath.Persian,
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                modelState.AddModelError(string.Empty, error.Description);
            }
            return null;
        }
        await users.AddToRoleAsync(user, Roles.Patient);

        var now = time.GetUtcNow().UtcDateTime;
        var profile = new PatientProfile { UserId = user.Id, CreatedByUserId = staffUserId, CreatedUtc = now };
        Apply(profile, input, dates, now);
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> UpdateAsync(ApplicationUser user, PatientInput input, PatientDates dates, ModelStateDictionary modelState)
    {
        var email = Clean(input.Email);
        user.FullName = input.FullName.Trim();
        user.PhoneNumber = NormalizeMobile(input.Mobile);
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            // Self-registered patients sign in with their email, which is also their user name.
            if (user.PasswordHash is not null && email is null)
            {
                modelState.AddModelError("Input.Email", l["This patient signs in with their email, so it cannot be removed."]);
                return false;
            }
            user.Email = email;
            user.EmailConfirmed = false;
            if (email is not null)
            {
                user.UserName = email;
            }
        }
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                modelState.AddModelError(string.Empty, error.Description);
            }
            return false;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var profile = await db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is null)
        {
            profile = new PatientProfile { UserId = user.Id, CreatedUtc = now };
            db.PatientProfiles.Add(profile);
        }
        Apply(profile, input, dates, now);
        await db.SaveChangesAsync();
        return true;
    }

    private static void Apply(PatientProfile p, PatientInput input, PatientDates dates, DateTime now)
    {
        p.NationalCode = Clean(JalaliDate.ToLatinDigits(input.NationalCode ?? ""));
        p.BirthDate = dates.Birth;
        p.FirstVisitDate = dates.FirstVisit;
        p.Children = input.Children;
        p.Gender = input.Gender;
        p.MaritalStatus = input.MaritalStatus;
        p.FatherName = Clean(input.FatherName);
        p.Occupation = Clean(input.Occupation);
        p.Education = Clean(input.Education);
        p.LandlinePhone = Clean(JalaliDate.ToLatinDigits(input.LandlinePhone ?? ""));
        p.City = Clean(input.City);
        p.Address = Clean(input.Address);
        p.PostalCode = Clean(JalaliDate.ToLatinDigits(input.PostalCode ?? ""));
        p.ReferralSource = Clean(input.ReferralSource);
        p.InsuranceProvider = Clean(input.InsuranceProvider);
        p.EmergencyContactName = Clean(input.EmergencyContactName);
        p.EmergencyContactPhone = Clean(JalaliDate.ToLatinDigits(input.EmergencyContactPhone ?? ""));
        p.Notes = Clean(input.Notes);
        p.UpdatedUtc = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>The model of the _ProfileSummary partial.</summary>
public sealed record ProfileSummary(ApplicationUser Patient, PatientProfile? Profile);

public readonly record struct PatientDates(DateOnly? Birth, DateOnly? FirstVisit);
