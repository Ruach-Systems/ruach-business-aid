using Microsoft.Data.SqlClient;
namespace Mashal.BusinessAid.Api.Data;

public sealed class SqlConnectionFactory(string connectionString)
{
    public async Task<SqlConnection> Open()
    {
        var connection = new SqlConnection(connectionString);
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
}
