# Backups

ShipLean can back itself up on a schedule to an S3 bucket you own, and this page is how to set that up and how to
restore from it. The restore runbook is written for a broken server: read the [runbook](#restore-runbook) first and
come back for the rest.

Everything ShipLean uploads is encrypted with a passphrase you choose. **If you lose the passphrase, the backups cannot
be opened. ShipLean never shows it again after you save it, so keep a copy in a password manager.**

## What is backed up

| Backed up | How |
|---|---|
| The database | A `pg_dump` (custom format) of the database in `SHIPLEAN_DB_CONNECTION`: every project and item, users, settings, the saved backup settings, and the ASP.NET Data Protection keys. With the keys back, sign-in cookies, agent tokens and the saved S3 secret key still decrypt after a restore. |
| Attachments and profile photos | Optional, off by default. Copied one object at a time, each encrypted. Only when they live outside the database: a folder (`SHIPLEAN_BLOB_ROOT`), an S3 bucket (`SHIPLEAN_BLOB_S3_CONNECTION`), or a Postgres blob store in another database. When the blob store is Postgres in the same database as the app, the dump already holds them and nothing is copied. |

Not backed up: Redis (it only holds live-event state), logs, and your environment variables. Keep your compose file or
equivalent yourself.

A backup is a copy of everything an admin can read, so treat the bucket like the database: private, and with access
limited to the people who may read the data.

## What lands in the bucket

Under the prefix you set (empty, or ending in `/`):

```
<prefix>database/shiplean-<yyyyMMddTHHmmssZ>-<id8>-g<n>.dump.slbk
<prefix>attachments/g<n>/<blob key>.slbk
```

- `database/…` is one file per run. The time is UTC. `<id8>` is the first 8 hex characters of the run id. Names sort
  oldest to newest.
- `g<n>` is the **passphrase generation** that encrypted the file. See [Passphrases](#passphrases).
- `attachments/g<n>/…` holds one file per attachment or photo.
- ShipLean writes a short-lived `<prefix>.shiplean-probe-<id>` object when you press **Test connection**, and deletes it.

The `.slbk` format is written up in [backup-file-format.md](backup-file-format.md).

## Set up

Open **Instance settings › Backups** (instance admins only). Fill in the destination, the schedule, how many to keep and
the passphrase, then **Save**. Press **Test connection** first: it writes, reads and deletes a test object and tells you
which step failed. **Back up now** runs one straight away.

Rules the form enforces:

- Bucket: an existing bucket. ShipLean does not create it.
- Prefix: optional, at most 256 characters, no leading `/`, no `//`. A trailing `/` is added for you.
- Keep the last: 1 to 365.
- Passphrase: 12 to 256 characters, typed twice. Spaces count.

### Permissions the key needs

The key needs these S3 permissions on the bucket and prefix, and nothing else:

| Permission | Why |
|---|---|
| `s3:PutObject` | Upload the dump and attachments. Multipart uploads (used above 64 MiB) are covered by it. |
| `s3:AbortMultipartUpload` | Clean up a multipart upload that failed part-way. Without it, failed uploads leave parts that cost money. |
| `s3:GetObject` | Test connection reads its object back, and a missing-or-present check reads object metadata. |
| `s3:DeleteObject` | Remove the test object, and old backups beyond **Keep the last**. |
| `s3:ListBucket` | List objects. **Also needed to tell a missing key from a forbidden one:** without it S3 answers 403 where it should answer 404, so the exists check and Test connection fail even though the key looks fine. |

### Amazon S3

Create a private bucket, then an IAM user (or role) with this policy. Replace `my-bucket` and `shiplean/prod/` with your
bucket and prefix. With no prefix, use `*` in both places where the prefix appears.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:GetObject", "s3:DeleteObject", "s3:AbortMultipartUpload"],
      "Resource": "arn:aws:s3:::my-bucket/shiplean/prod/*"
    },
    {
      "Effect": "Allow",
      "Action": "s3:ListBucket",
      "Resource": "arn:aws:s3:::my-bucket",
      "Condition": { "StringLike": { "s3:prefix": "shiplean/prod/*" } }
    }
  ]
}
```

In ShipLean: choose the region, set the bucket and the prefix (`shiplean/prod/`), and enter the access key id and secret
key. Leave both key fields empty to use the AWS default credentials of the server (an IAM role, environment variables
or a profile). Leave the endpoint empty.

### Ceph RGW and other S3-compatible stores

Choose **Custom endpoint** and set:

- Endpoint: the full address, `http://` or `https://`, with no keys, query or `#` in it. A plain-http or private
  address is allowed.
- Region: the signing region the store expects, often `us-east-1`. Ceph RGW with the default zonegroup accepts
  `us-east-1`.
- Force path style: on. Most non-AWS stores need `https://host/bucket/key`, not `https://bucket.host/key`.
- Bucket, prefix and keys as above. Grant the user the same five permissions through the store's own policy tool.

### Cloudflare R2

Create a bucket and an API token with **Object Read & Write** on that bucket. Then set:

- Endpoint: `https://<account id>.r2.cloudflarestorage.com` (your account id is on the R2 page).
- Region: `auto`.
- Force path style: on.
- Access key id and secret key: the token's.

### Schedule

The schedule is a cron expression with five fields (minute, hour, day of month, month, day of week) in **UTC**. The
form shows a plain-words description and the next five run times as you type.

| Cron | Meaning |
|---|---|
| `0 2 * * *` | Every day at 02:00 UTC (the default) |
| `0 */6 * * *` | Every 6 hours |
| `30 1 * * 1` | Mondays at 01:30 UTC |
| `0 3 1 * *` | The 1st of each month at 03:00 UTC |

Runs must be at least one hour apart. A schedule that breaks that on any day in the next 400 days is refused, and so is
one that never runs.

How it behaves:

- Every ShipLean instance watches the schedule, and each occurrence starts once. Only one backup runs at a time.
- If another run is busy when an occurrence comes, ShipLean retries every 5 minutes until the next occurrence is due.
  Then it gives up on the old one and moves on.
- If no instance was running over several occurrences, they are skipped and one backup runs at the next one. It does
  not catch up one by one.
- A new or changed schedule takes effect within a minute, and never runs occurrences from before it was saved.

### Keeping the last N

After each successful run, ShipLean counts this destination's successful database backups and deletes the database
files beyond **Keep the last N**, oldest first. Only `database/…` files are deleted. Attachment copies are never
deleted, so they only grow.

If you change the bucket, prefix or endpoint, the count starts again for the new destination. Backups in the old one
are left alone.

If a delete fails (for example the bucket is locked), the run is still a success. The old file stays in the bucket, the
run is marked pruned, and the server log names the key under `Could not delete old backup`. Delete it by hand.

### Passphrases

Saving a new passphrase starts the next **generation**. Typing the same passphrase again does not. Each file carries
its generation in its name (`-g3`), so you know which passphrase opens it:

- Backups made before the change keep the old passphrase, and stay in the bucket until pruned. Keep every passphrase
  that still has a file in the bucket.
- After a change, attachments are copied again under the new generation, into `attachments/g<n+1>/`. Old generations
  stay where they are until you remove them.
- Restore a dump and its attachments from the **same generation**: the `-g<n>` in the dump's name picks the
  `attachments/g<n>/` folder.

The passphrase is stored in the database under Data Protection, like the S3 secret key. Both are only ever shown as
"Passphrase #3, set …" and the last four characters of the secret key.

### A second line of defence

ShipLean deletes old backups itself, and anyone with the key can delete everything. Add one of these on the bucket:

- **Versioning**, with a lifecycle rule that expires old versions after the period you want. A delete then only hides
  the file.
- **Object lock** in compliance or governance mode. Note that a locked file cannot be pruned, so the prune warning
  above will appear until the lock runs out. Delete those files by hand after that.
- A lifecycle rule that aborts incomplete multipart uploads after a day or two.

Use a bucket in another account or region when you can.

## Restore runbook

You need: Docker, the passphrase (the one for the generation in the file name), the bucket's access, and a Postgres 18
server or newer that you can reach. Everything below runs in a shell, from an empty working folder.

### 1. Stop ShipLean

Stop every ShipLean instance that points at the database, so nothing writes while you restore and nothing migrates an
empty database:

```bash
docker compose -f src/docker-compose.yml stop shiplean
```

### 2. Download the newest database backup

With the AWS CLI. For a non-AWS store also `export AWS_ENDPOINT_URL=https://your-endpoint`. Set the bucket and prefix
as you saved them (`PREFIX=""` when there is none):

```bash
BUCKET=my-bucket
PREFIX=shiplean/prod/
DUMP_FILE=$(aws s3 ls "s3://$BUCKET/${PREFIX}database/" | awk '{print $4}' | sort | tail -n 1)
echo "$DUMP_FILE"
aws s3 cp "s3://$BUCKET/${PREFIX}database/$DUMP_FILE" "$DUMP_FILE"
GEN=${DUMP_FILE##*-g}; GEN=${GEN%%.*}
echo "passphrase generation $GEN"
```

`GEN` is the generation. You need that passphrase. If this backup is damaged or newer than you want, pick an older
name from `aws s3 ls` and set `DUMP_FILE` to it before the `aws s3 cp`.

### 3. Decrypt the dump

Read the passphrase into a shell variable without echoing it, so it stays out of shell history and the screen. Use the
image of the version you ran, or `latest`:

```bash
read -rs SHIPLEAN_BACKUP_PASSPHRASE && export SHIPLEAN_BACKUP_PASSPHRASE
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work" -e SHIPLEAN_BACKUP_PASSPHRASE \
  ghcr.io/shiplean/shiplean decrypt-backup --in "/work/$DUMP_FILE" --out /work/shiplean.dump
```

`-e SHIPLEAN_BACKUP_PASSPHRASE` passes the variable through without putting its value on the command line.

To pipe the passphrase on standard input instead, use `-i`. It reads one line:

```bash
printf '%s\n' "$SHIPLEAN_BACKUP_PASSPHRASE" | docker run --rm -i --user "$(id -u):$(id -g)" -v "$PWD:/work" \
  ghcr.io/shiplean/shiplean decrypt-backup --in "/work/$DUMP_FILE" --out /work/shiplean.dump
```

A file with the passphrase alone on one line also works: `docker run --rm -i … < passphrase.txt`. **Do not run `docker run -i` without a pipe and type the passphrase:** without `-t` there is no prompt, and
what you type is echoed to the terminal.

It prints `Decrypted … -> /work/shiplean.dump`. A wrong passphrase prints an error and exits 1, and writes nothing.
The command never overwrites: if `shiplean.dump` exists, it stops. The file is readable only by you.

### 4. Restore the database

Set where the database is. `NETWORK` is the Docker network that reaches Postgres (`docker network ls`; for the compose
file in `src/` it is `src_default`). `ADMIN_URL` connects to the server's `postgres` database. `DATABASE_URL` is the
database ShipLean will use, and `DB_NAME` its name:

```bash
NETWORK=src_default
DB_NAME=shiplean
ADMIN_URL=postgresql://postgres:secret@postgres:5432/postgres
DATABASE_URL=postgresql://postgres:secret@postgres:5432/shiplean
```

Restore into an **empty** database. The dump has no `DROP` statements, so a database that already has ShipLean tables
(ShipLean started once and migrated it) fails with "already exists" errors. If it has, drop it first:

```bash
docker run --rm --network "$NETWORK" postgres:18 psql "$ADMIN_URL" -c "DROP DATABASE IF EXISTS $DB_NAME WITH (FORCE)"
docker run --rm --network "$NETWORK" postgres:18 psql "$ADMIN_URL" -c "CREATE DATABASE $DB_NAME"
```

Skip the drop and the create if the database exists and is empty (a fresh compose volume creates an empty `shiplean`).
Then restore with the `pg_restore` from the `postgres:18` image:

```bash
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work" --network "$NETWORK" \
  postgres:18 pg_restore --no-owner --dbname="$DATABASE_URL" /work/shiplean.dump
```

`--user` is there because the decrypted file is readable only by you. The dump holds the schema, the data, the
`schemaversions` table (so ShipLean's startup migrations skip what is applied) and the Data Protection keys. It holds no
owners and no privileges (it was made with `--no-owner --no-privileges`), so the objects belong to the user in
`DATABASE_URL`, and no `GRANT` is replayed: give a different application user its rights yourself.

Use `pg_restore` 18 or newer. An older one refuses the file.

### 5. Restore attachments (only if you backed them up)

Skip this when attachments were not included, or when the blob store is Postgres in the app's own database (the dump
had them).

Download the folder of the **same generation** as the dump, and decrypt-and-put it into the blob store the instance
uses (the same `SHIPLEAN_BLOB_*` setting it ran with):

```bash
aws s3 sync "s3://$BUCKET/${PREFIX}attachments/g$GEN/" "attachments/g$GEN/"
```

Then run the one command that matches your store. `restore-blobs` decrypts each file, puts its blob under its original
key and content type, and **skips keys already in the store** unless you add `--overwrite`. It prints
`Restored N, skipped N (already in the store), failed N.` and exits 1 if any failed. Rerunning is safe.

**Folder (`SHIPLEAN_BLOB_ROOT`).** `BLOBS_VOLUME` is the volume ShipLean mounts at `/data/blobs` (`docker volume ls`; for
the compose file in `src/` it is `src_shiplean-blobs`). The command runs as the image's own user, so the files belong
to the user ShipLean runs as; the downloaded files must be readable by everyone (the default):

```bash
BLOBS_VOLUME=src_shiplean-blobs
docker run --rm -v "$PWD:/work:ro" -v "$BLOBS_VOLUME:/data/blobs" \
  -e SHIPLEAN_BLOB_ROOT=/data/blobs -e SHIPLEAN_BACKUP_PASSPHRASE \
  ghcr.io/shiplean/shiplean restore-blobs --in "/work/attachments/g$GEN"
```

**Postgres in another database (`SHIPLEAN_BLOB_DB_CONNECTION`).** Only needed when the blobs are in a different
database from the app's. The command never starts ShipLean and never runs migrations, so that database **must already
have the `blob` table**. On a new database, start ShipLean once with `SHIPLEAN_DB_MIGRATE` on and the same
`SHIPLEAN_BLOB_DB_CONNECTION` (it creates the table), stop it, then run:

```bash
BLOB_DB_CONNECTION="Host=postgres;Port=5432;Database=shiplean_blobs;Username=postgres;Password=secret"
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work:ro" --network "$NETWORK" \
  -e SHIPLEAN_BLOB_DB_CONNECTION="$BLOB_DB_CONNECTION" -e SHIPLEAN_BACKUP_PASSPHRASE \
  ghcr.io/shiplean/shiplean restore-blobs --in "/work/attachments/g$GEN"
```

Without the table it exits 1 with an error from Postgres for every file. This is an Npgsql connection string, not a
URL.

**S3 (`SHIPLEAN_BLOB_S3_CONNECTION`).** The bucket must exist. Use the value ShipLean runs with, as in the
[README](../README.md#configuration). The key needs read, write and list on that bucket:

```bash
BLOB_S3_CONNECTION="bucket=shiplean;region=ap-southeast-1;accessKey=AKIA…;secretKey=…;prefix=prod/"
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/work:ro" --network "$NETWORK" \
  -e SHIPLEAN_BLOB_S3_CONNECTION="$BLOB_S3_CONNECTION" -e SHIPLEAN_BACKUP_PASSPHRASE \
  ghcr.io/shiplean/shiplean restore-blobs --in "/work/attachments/g$GEN"
```

(`--network` is only needed when the store is on a Docker network; drop it for a public endpoint.)

If the setting is wrong the command exits 1 with `blob store from SHIPLEAN_BLOB_* could not be opened`. With none of the
three set it uses the default folder inside the container, which is thrown away when the command ends: always set one.
If more than one is set, S3 wins, then Postgres, then the folder, the same rule as the app.

### 6. Start ShipLean and check

```bash
docker compose -f src/docker-compose.yml up -d shiplean
curl -fsS http://127.0.0.1:8080/health
```

`/health` answers 200 with the version. Sign in and open **Instance settings › Backups**. The saved destination is
still there, with its secret key (the Data Protection keys came back with the dump). If you restored to a different
host, check the setting there: **Test connection** proves the key still works.

Now delete the decrypted file: `shred -u shiplean.dump` (or `rm`). It holds the keys and every item in plain text.

Because the restore brings back the old schedule, ShipLean may start a backup within a minute or so if one is overdue.
That is expected.

## Troubleshooting

### `decrypt-backup` and `restore-blobs`

| Exit | Meaning |
|---|---|
| 0 | Done. |
| 1 | Wrong passphrase, a damaged or cut-off file, or a blob store error. The message names the file and never the passphrase. |
| 2 | Wrong arguments, no passphrase (set `SHIPLEAN_BACKUP_PASSPHRASE` or give one line on stdin), a missing `--in`, or `--out` already in use. Nothing was written. |

- **Exit 1 on every file:** the passphrase belongs to a different generation. Check `-g<n>` in the name.
- **Exit 1 on one file:** that file is damaged or was uploaded in part. Download it again. If it still fails, restore
  from an older backup.
- **`already exists (or its .partial file does)`:** remove the old output first. `decrypt-backup` never overwrites, and
  it deletes its `.partial` file when a decrypt fails.
- **Permission denied writing `/work`:** run with `--user "$(id -u):$(id -g)"` and a folder you own.

### `pg_dump` version mismatch

A run fails with `The database dump failed: pg_dump failed with exit code 1: pg_dump: error: aborting because of server
version mismatch`. `pg_dump` must be at least the database server's major version. The image ships 18. Fix: use the
current image, or upgrade the client. On a build you made yourself, install `postgresql-client-18` and point
`SHIPLEAN_BACKUP_PG_DUMP` at its `pg_dump`. `pg_dump was not found at '…'` means the same setting is wrong or the
client is not installed.

### A failed run

The run history on the Backups tab shows the error. It starts with the step that failed:

| Message starts with | Look at |
|---|---|
| `The destination could not be set up` | The settings: bucket, endpoint, keys. Run **Test connection**. |
| `The temporary file could not be created` | `SHIPLEAN_BACKUP_TEMP`: free space and who owns the folder. It needs room for one encrypted dump. |
| `The encryption could not start` | Rare. Check the server log. |
| `The database dump failed` | The text after it is `pg_dump`'s own error (the password is removed). |
| `The upload failed` | Permissions, a full or deleted bucket, or the network. S3 error codes are shown: `AccessDenied` and HTTP 403 mean a missing permission (see the [list](#permissions-the-key-needs), including `s3:ListBucket`). |
| `Copying the attachments failed` | The blob store or the destination. The dump itself was uploaded. |
| `Recording the run failed` | The database. The upload may have worked. |

Messages never contain a secret. The server log has more, under `Backup run <id> failed`.

### Health

The health line on the Backups tab:

| Health | Meaning |
|---|---|
| Healthy | The last run worked and nothing is overdue. |
| Backing up now | A run is going. |
| Last backup failed | The newest finished run failed. The next success clears it. |
| Overdue | The schedule is on, and an occurrence came more than an hour ago with no success since. Look for a stopped server, or runs that were skipped because a manual run was busy. The log says `Scheduled backups from … were skipped`. |
| No backups yet | Set up, but nothing has run. |
| Not set up | Nothing saved. |

Backups are not part of `GET /health`, which only says the app is up.

### An abandoned run

A run whose server stopped (crash, restart, a deploy) while it was going shows as failed with `Abandoned: the instance
running it stopped.` ShipLean marks it so the next time any instance takes the backup lock, and counts it as a failed
run until then. Run **Back up now**. If the run was in the middle of an upload, any parts are left in the bucket: add
the lifecycle rule above. If another instance may still be running, the log says `the backup lease was lost`: that run
stopped, and a later run does the backup.

## Metrics

The `shiplean.backup.runs` counter (tag `outcome`: `succeeded` or `failed`) counts runs that ended. See
[observability.md](observability.md#metrics).
