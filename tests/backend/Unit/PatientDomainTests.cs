using SmartHealthcare.Patients.Application;
using SmartHealthcare.Patients.Domain;

namespace SmartHealthcare.UnitTests;

public class PatientDomainTests
{
    private static readonly DateOnly Today = new(2026, 10, 29);

    private static PatientDetails Valid(string? nationalId = null) =>
        PatientDetails.Create("  Nguyễn   Văn  An ", new DateOnly(1990, 5, 1), "male", nationalId, "090 123 4567", null, Today).Value!;

    [Fact]
    public void Details_normalize_name_phone_and_gender()
    {
        var details = Valid();

        Assert.Equal("Nguyễn Văn An", details.FullName);
        Assert.Equal("0901234567", details.PhoneNumber);
        Assert.Equal(Gender.Male, details.Gender);
    }

    [Theory]
    [InlineData("2026-10-30", "dateOfBirth")]
    [InlineData("1899-12-31", "dateOfBirth")]
    public void Details_reject_birth_date_outside_range(string dob, string field)
    {
        var result = PatientDetails.Create("Trần Bình", DateOnly.Parse(dob), "Female", null, null, null, Today);

        Assert.False(result.IsSuccess);
        Assert.Contains(field, result.Error.Fields!.Keys);
    }

    [Fact]
    public void Details_collect_every_invalid_field()
    {
        var result = PatientDetails.Create("A", null, "1", "123", "12345", new string('x', 301), Today);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            new[] { "address", "dateOfBirth", "fullName", "gender", "nationalId", "phoneNumber" },
            result.Error.Fields!.Keys.Order());
    }

    [Fact]
    public void Numeric_enum_text_is_rejected()
    {
        Assert.False(DomainEnum.TryParse<Gender>("1", out _));
        Assert.True(DomainEnum.TryParse<Relationship>("SELF", out var r));
        Assert.Equal(Relationship.Self, r);
    }

    [Fact]
    public void National_id_can_be_added_once_but_not_changed()
    {
        var patient = Patient.Create(Valid(), DateTime.UtcNow);

        Assert.True(patient.Update(Valid("001090000001")).IsSuccess);
        Assert.Equal("001090000001", patient.NationalId);

        Assert.True(patient.Update(Valid()).IsSuccess);
        Assert.Equal("001090000001", patient.NationalId);

        var change = patient.Update(Valid("001090000002"));
        Assert.False(change.IsSuccess);
        Assert.Equal("national_id_locked", change.Error.Code);
        Assert.Equal("001090000001", patient.NationalId);
    }

    [Fact]
    public void Creator_link_grants_access_until_revoked()
    {
        var link = PatientRepresentative.ForProfileCreator(Guid.NewGuid(), Guid.NewGuid(), Relationship.Child, DateTime.UtcNow);
        Assert.True(link.GrantsAccess);

        link.Revoke(DateTime.UtcNow);

        Assert.False(link.GrantsAccess);
        Assert.Equal(VerificationStatus.Revoked, link.VerificationStatus);
        Assert.NotNull(link.RevokedAtUtc);
    }

    [Theory]
    [InlineData("001090000123", "********0123")]
    [InlineData(null, null)]
    public void National_id_is_masked_except_last_four_digits(string? input, string? expected) =>
        Assert.Equal(expected, PatientProfileDto.MaskNationalId(input));
}
