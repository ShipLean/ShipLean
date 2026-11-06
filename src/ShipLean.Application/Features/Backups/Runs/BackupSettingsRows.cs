using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Backups.Runs;

// The one backup_settings row (id 1), raw: secrets stay protected. TryWriteAsync is optimistic on updated_at, so two
// concurrent saves can't both hand out the next passphrase generation: expectedUpdatedAt null means "no row yet".
[RegisterSingleton]
public sealed class BackupSettingsRows(NpgsqlDataSource dataSource)
{
    public async Task<BackupSettingsRow?> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BackupSettingsRow>(new CommandDefinition(
            """
            SELECT enabled AS Enabled, provider AS Provider, destination::text AS Destination, destination_secret AS DestinationSecret,
                   secret_last4 AS SecretLast4, cron AS Cron, keep_count AS KeepCount, include_attachments AS IncludeAttachments,
                   passphrase AS Passphrase, passphrase_generation AS PassphraseGeneration, passphrase_set_at AS PassphraseSetAt,
                   updated_at AS UpdatedAt, updated_by AS UpdatedBy
            FROM backup_settings WHERE id = 1
            """,
            cancellationToken: cancellationToken));
    }

    // False when the row changed (or appeared) since expectedUpdatedAt was read.
    public async Task<bool> TryWriteAsync(BackupSettingsRow row, DateTimeOffset? expectedUpdatedAt, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            row.Enabled, row.Provider, row.Destination, row.DestinationSecret, row.SecretLast4, row.Cron, row.KeepCount, row.IncludeAttachments,
            row.Passphrase, row.PassphraseGeneration, PassphraseSetAt = row.PassphraseSetAt?.ToUniversalTime(), UpdatedAt = row.UpdatedAt.ToUniversalTime(),
            row.UpdatedBy, Expected = expectedUpdatedAt?.ToUniversalTime(),
        };
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var written = expectedUpdatedAt is null
            ? await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO backup_settings (id, enabled, provider, destination, destination_secret, secret_last4, cron, keep_count, include_attachments,
                                             passphrase, passphrase_generation, passphrase_set_at, updated_at, updated_by)
                VALUES (1, @Enabled, @Provider, @Destination::jsonb, @DestinationSecret, @SecretLast4, @Cron, @KeepCount, @IncludeAttachments,
                        @Passphrase, @PassphraseGeneration, @PassphraseSetAt, @UpdatedAt, @UpdatedBy)
                ON CONFLICT (id) DO NOTHING
                """,
                parameters, cancellationToken: cancellationToken))
            : await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE backup_settings
                SET enabled = @Enabled, provider = @Provider, destination = @Destination::jsonb, destination_secret = @DestinationSecret,
                    secret_last4 = @SecretLast4, cron = @Cron, keep_count = @KeepCount, include_attachments = @IncludeAttachments,
                    passphrase = @Passphrase, passphrase_generation = @PassphraseGeneration, passphrase_set_at = @PassphraseSetAt,
                    updated_at = @UpdatedAt, updated_by = @UpdatedBy
                WHERE id = 1 AND updated_at = @Expected
                """,
                parameters, cancellationToken: cancellationToken));
        return written == 1;
    }
}
