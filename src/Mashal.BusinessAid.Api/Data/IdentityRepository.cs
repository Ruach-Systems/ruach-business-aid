using Dapper;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Api.Data;

public sealed class IdentityRepository(SqlConnectionFactory connections, IConfiguration? configuration = null)
{
    public bool IsAdmin(string email, bool verified) => verified && (configuration?.GetSection("MashalAdmin:Emails").Get<string[]>() ?? [])
        .Any(allowed => string.Equals(allowed.Trim(), email, StringComparison.OrdinalIgnoreCase));
    public async Task<AppUser> SignIn(string subject, string name, string email, string? photo, bool verified = false)
    {
        await using var c = await connections.Open();
        await using var tx = c.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var id = await c.QuerySingleOrDefaultAsync<Guid?>("SELECT Id FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE GoogleSubject=@subject", new { subject }, tx);
        if (id is null)
        {
            id = Guid.NewGuid();
            await c.ExecuteAsync("INSERT dbo.Users(Id,GoogleSubject,DisplayName,Email,PhotoURL,EmailVerified) VALUES(@id,@subject,@name,@email,@photo,@verified)", new { id, subject, name, email, photo, verified }, tx);
        }
        else await c.ExecuteAsync("UPDATE dbo.Users SET DisplayName=@name,Email=@email,PhotoURL=@photo,EmailVerified=@verified WHERE Id=@id", new { id, name, email, photo, verified }, tx);
        await tx.CommitAsync();
        return (await Find(id.Value))!;
    }
    public async Task<AppUser?> Find(Guid id)
    {
        await using var c = await connections.Open();
        var row = await c.QuerySingleOrDefaultAsync<UserRow>("SELECT Id,DisplayName,Email,PhotoURL,PhoneNumber,EmailVerified FROM dbo.Users WHERE Id=@id", new { id });
        return row is null ? null : new AppUser(row.Id, row.DisplayName, row.Email, row.PhotoURL) { PhoneNumber = row.PhoneNumber, IsAdmin = IsAdmin(row.Email, row.EmailVerified) };
    }
    private sealed record UserRow(Guid Id, string DisplayName, string Email, string? PhotoURL, string? PhoneNumber, bool EmailVerified);
}
