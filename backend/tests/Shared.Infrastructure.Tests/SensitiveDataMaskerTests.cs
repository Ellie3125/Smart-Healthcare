using Shared.Infrastructure.Persistence;
using Xunit;

namespace Shared.Infrastructure.Tests;

public class SensitiveDataMaskerTests
{
    [Fact]
    public void Masks_national_id_and_phone_keeping_last_three_characters()
    {
        Assert.Equal("""{"NationalId":"*********567","PhoneNumber":"*******678","FullName":"Nguyễn Văn A"}""",
            SensitiveDataMasker.MaskJson("""{"NationalId":"001203004567","PhoneNumber":"0912345678","FullName":"Nguyễn Văn A"}"""));
    }

    [Fact]
    public void Matches_keys_case_insensitively_at_every_level()
    {
        Assert.Equal("""{"patient":{"nationalid":"*********567","contacts":[{"PHONENUMBER":"*******678"}]}}""",
            SensitiveDataMasker.MaskJson("""{"patient":{"nationalid":"001203004567","contacts":[{"PHONENUMBER":"0912345678"}]}}"""));
    }

    [Theory]
    [InlineData("""{"PhoneNumber":"123"}""", """{"PhoneNumber":"***"}""")]
    [InlineData("""{"PhoneNumber":""}""", """{"PhoneNumber":""}""")]
    [InlineData("""{"PhoneNumber":null}""", """{"PhoneNumber":null}""")]
    [InlineData("""{"PhoneNumber":912345678}""", """{"PhoneNumber":"******678"}""")]
    public void Short_null_and_numeric_values(string input, string expected)
    {
        Assert.Equal(expected, SensitiveDataMasker.MaskJson(input));
    }

    [Fact]
    public void Null_json_stays_null()
    {
        Assert.Null(SensitiveDataMasker.MaskJson(null));
    }
}
