using Dapper;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Api.Data;

public sealed class IdentityRepository(SqlConnectionFactory connections)
{
    public async Task<AppUser> SignIn(string subject, string name, string email, string? photo)
    {
        await using var c = await connections.Open();
        await using var tx = c.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var id = await c.QuerySingleOrDefaultAsync<Guid?>("SELECT Id FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE GoogleSubject=@subject", new { subject }, tx);
        if (id is null)
        {
            id = Guid.NewGuid();
            await c.ExecuteAsync("INSERT dbo.Users(Id,GoogleSubject,DisplayName,Email,PhotoURL) VALUES(@id,@subject,@name,@email,@photo)", new { id, subject, name, email, photo }, tx);
        }
        else await c.ExecuteAsync("UPDATE dbo.Users SET DisplayName=@name,Email=@email,PhotoURL=@photo WHERE Id=@id", new { id, name, email, photo }, tx);
        await tx.CommitAsync();
        return new AppUser(id.Value, name, email, photo);
    }
    public async Task<AppUser?> Find(Guid id)
    {
        await using var c = await connections.Open();
        return await c.QuerySingleOrDefaultAsync<AppUser>("SELECT Id Uid,DisplayName,Email,PhotoURL FROM dbo.Users WHERE Id=@id", new { id });
    }
}
