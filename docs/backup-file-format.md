# Backup file format: SLBK1

Everything ShipLean uploads to a backup destination is encrypted with the backup passphrase in the `SLBK1` format:
the database dump (`database/….dump.slbk`, the plaintext is `pg_dump` custom format) and each attachment
(`attachments/g<gen>/<key>.slbk`, the plaintext is one `BlobEnvelope`). The code is
`src/ShipLean.Application/Features/Backups/Crypto/` (`AesGcmBackupCipher`); its tests,
`src/ShipLean.WebApi.Tests/Backups/Crypto/`, include `Slbk1Spec`, a reader and writer built from this page alone.

All integers are unsigned and big-endian.

## Header (36 bytes)

| Offset | Size | Field | Value |
|---|---|---|---|
| 0 | 4 | magic | ASCII `SLBK` |
| 4 | 1 | version | `0x01` |
| 5 | 1 | key derivation | `0x01` = PBKDF2-HMAC-SHA256 |
| 6 | 4 | iterations | written as 600,000 |
| 10 | 16 | salt | random per file |
| 26 | 8 | nonce prefix | random per file |
| 34 | 2 | reserved | `0x00 0x00` |

A reader refuses the file, before deriving any key, when the magic, version or key derivation differ, when the
iterations are below 600,000 or above 2,000,000, or when either reserved byte is not zero.

## Key

`key = PBKDF2-HMAC-SHA256(password = UTF-8 bytes of the passphrase, salt, iterations, 32 bytes)`. The cipher is
AES-256-GCM with a 12-byte nonce and a 16-byte tag.

## Records

The header is followed by one or more records. Record `i` (counting from 0) is:

| Size | Field |
|---|---|
| 1 | flags: `0x01` = final, `0x00` = not final; no other bit may be set |
| 4 | plaintext length `n`, at most 1,048,576 (1 MiB) |
| `n` | ciphertext |
| 16 | GCM tag |

- **Nonce** (12 bytes): the header's nonce prefix (8), then `i` as uint32 (4).
- **AAD** (41 bytes): the whole 36-byte header, then `i` as uint32 (4), then the flags byte (1).

So a record cannot be moved, dropped, repeated, re-flagged or put under another header without its tag failing.

The writer buffers plaintext until it has a full 1 MiB record, writes that record (not final) as soon as it fills,
and on close writes what is left as the final record. The final record can be empty: an empty input, or an input that
is an exact multiple of 1 MiB, ends with a final record of length 0. The file length is therefore
`36 + Σ (5 + n + 16)`.

A reader refuses:

- a record whose tag does not verify (a wrong passphrase fails on record 0, before any plaintext is released);
- a length over 1 MiB, from the record header, before reading or allocating for the body;
- unknown flag bits;
- a stream that ends before a final record (cut off), including one that ends inside a record;
- any byte after the final record;
- a record index past 2³² − 1 (the writer refuses to write one too).

## Releasing plaintext

A record's plaintext is written out only after its tag verifies, but earlier records have been written out by then.
So a reader that fails part-way has written a prefix of the plaintext. Callers decrypt into a temporary file and use
it only once decryption has returned without error (`decrypt-backup` writes `<out>.partial` and renames it on success).

Memory use is about 2 MiB (one plaintext record and one sealed record) at any file size, for both writing and reading.

## Decrypting by hand

The `decrypt-backup` command of the ShipLean image is the supported way (`decrypt-backup --in <file|folder> --out
<file|folder>`, with the passphrase from `SHIPLEAN_BACKUP_PASSPHRASE` or one line of stdin). Any language with PBKDF2 and AES-GCM can follow this
page instead: read the header, check it, derive the key, then for each record build the nonce and AAD and decrypt.
