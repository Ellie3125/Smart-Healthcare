using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace SqlServerTests;

public sealed class IdentityPatientContactsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Batch_contacts_read_once_mask_the_phone_and_omit_missing_patients()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        var first = await fixture.InsertAsync("identity.Patient", new { FullName = "Test patient", PhoneNumber = "0901234567" });
        var shortPhone = await fixture.InsertAsync("identity.Patient", new { FullName = "Short phone", PhoneNumber = "12" });
        var count = new QueryCount();
        await using var db = fixture.CreateContext<IdentityPatientsDbContext>(o => new(new DbContextOptionsBuilder<IdentityPatientsDbContext>(o).AddInterceptors(count).Options));
        IIdentityPatientsModule module = new IdentityPatientsModule(db);
        var contacts = await module.GetPatientContactsAsync([first, shortPhone, first, Guid.NewGuid()], Ct);
        Assert.Equal(1, count.Count);
        Assert.Equal(2, contacts.Count);
        Assert.Equal("*******567", Assert.Single(contacts, x => x.PatientId == first).MaskedPhoneNumber);
        Assert.Equal("Test patient", Assert.Single(contacts, x => x.PatientId == first).FullName);
        Assert.Equal("**", Assert.Single(contacts, x => x.PatientId == shortPhone).MaskedPhoneNumber);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await module.GetPatientContactsAsync([Guid.NewGuid()], Ct));
        var before = count.Count;
        Assert.Empty(await module.GetPatientContactsAsync([], Ct));
        Assert.Equal(before, count.Count);
    }
    [Fact]
    public async Task Contact_batch_rejects_invalid_ids_and_more_than_a_page()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<IdentityPatientsDbContext>(o => new(o));
        IIdentityPatientsModule module = new IdentityPatientsModule(db);
        await Assert.ThrowsAsync<ArgumentException>(() => module.GetPatientContactsAsync([Guid.Empty], Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => module.GetPatientContactsAsync(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray(), Ct));
    }
    private sealed class QueryCount : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }
}
