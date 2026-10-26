using System.Xml.Linq;
using Dapper;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Npgsql;

namespace ShipLean.WebApi.Features.Auth;

// Data Protection's key ring in the data_protection_key table, so a redeploy does not sign everyone out.
public sealed class PostgresXmlRepository(NpgsqlDataSource dataSource) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = dataSource.OpenConnection();
        return connection.Query<string>("SELECT xml FROM data_protection_key ORDER BY id").Select(XElement.Parse).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = dataSource.OpenConnection();
        connection.Execute(
            "INSERT INTO data_protection_key (friendly_name, xml) VALUES (@friendlyName, @xml)",
            new { friendlyName, xml = element.ToString(SaveOptions.DisableFormatting) });
    }
}
