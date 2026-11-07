using System.Text.RegularExpressions;
using Amazon;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Backups.Admin;

// What a save or a connection test may send, checked before anything is read or written. Every error is a validation
// ApiException whose Field is the DTO path, and no message repeats the value (an endpoint URL may hold a password).
public static partial class BackupSaveRules
{
    public const string DestinationField = "destination";
    public const string ProviderField = "destination.provider";
    public const string S3Field = "destination.s3";
    public const string BucketField = "destination.s3.bucket";
    public const string RegionField = "destination.s3.region";
    public const string EndpointField = "destination.s3.endpointUrl";
    public const string PrefixField = "destination.s3.prefix";
    public const string AccessKeyIdField = "destination.s3.accessKeyId";
    public const string SecretKeyField = "destination.s3.secretKey";
    public const string CronField = "cron";
    public const string KeepCountField = "keepCount";
    public const string PassphraseField = "passphrase";

    public const int MinKeepCount = 1;
    public const int MaxKeepCount = 365;
    public const int MinPassphrase = 12;
    public const int MaxPassphrase = 256;

    private const int MaxPrefix = 256;
    private const int MaxKey = 256;

    private static readonly string[] BucketPrefixes = ["xn--", "sthree-", "amzn-s3-demo-"];
    private static readonly string[] BucketSuffixes = ["-s3alias", "--ol-s3", ".mrap", "--x-s3", "--table-s3"];

    // The destination trimmed and normalised: a blank endpoint or access key ID is null, a non-empty prefix ends in "/".
    // SecretKey stays as sent: null keeps the saved one, "" clears it.
    public static S3DestinationSaveDto Destination(BackupDestinationSaveDto? destination)
    {
        if (destination is null)
        {
            throw ApiException.Validation("Choose where backups go.", DestinationField);
        }
        var s3 = destination.Provider switch
        {
            BackupProvider.S3 => destination.S3 ?? throw ApiException.Validation("Enter the S3 settings.", S3Field),
            _ => throw ApiException.Validation("Choose a supported destination.", ProviderField),
        };
        var bucket = Bucket(s3.Bucket);
        var endpoint = Endpoint(s3.EndpointUrl);
        var region = Region(s3.Region, endpoint);
        var prefix = Prefix(s3.Prefix);
        var accessKeyId = Blank(s3.AccessKeyId);
        if (accessKeyId is not null && !IsOneLine(accessKeyId))
        {
            throw ApiException.Validation($"The access key ID must be one line of at most {MaxKey} characters with no spaces.", AccessKeyIdField);
        }
        if (s3.SecretKey is { Length: 0 } && accessKeyId is not null)
        {
            throw ApiException.Validation("Remove the access key ID too: with no keys, the AWS default credential chain is used.", AccessKeyIdField);
        }
        if (s3.SecretKey is { Length: > 0 } secret)
        {
            if (!IsOneLine(secret))
            {
                throw ApiException.Validation($"The secret key must be one line of at most {MaxKey} characters with no spaces.", SecretKeyField);
            }
            if (accessKeyId is null)
            {
                throw ApiException.Validation("Enter the access key ID that goes with this secret key.", AccessKeyIdField);
            }
        }
        return new S3DestinationSaveDto(region, endpoint, bucket, prefix, s3.ForcePathStyle, accessKeyId, s3.SecretKey);
    }

    public static string Cron(string? cron, ICronSchedule schedule)
    {
        var expression = cron?.Trim() ?? throw ApiException.Validation(CronSchedule.WrongFieldsError, CronField);
        if (schedule.Validate(expression) is { } error)
        {
            throw ApiException.Validation(error, CronField);
        }
        return expression;
    }

    public static int KeepCount(int keepCount) => keepCount is < MinKeepCount or > MaxKeepCount
        ? throw ApiException.Validation($"Keep between {MinKeepCount} and {MaxKeepCount} backups.", KeepCountField)
        : keepCount;

    // A new passphrase as typed (never trimmed: spaces count), or null to keep the saved one.
    public static string? Passphrase(string? passphrase, bool saved)
    {
        if (passphrase is null)
        {
            return saved ? null : throw ApiException.Validation("Set a passphrase: every backup is encrypted with it.", PassphraseField);
        }
        if (passphrase.Length is < MinPassphrase or > MaxPassphrase || string.IsNullOrWhiteSpace(passphrase))
        {
            throw ApiException.Validation($"The passphrase must be {MinPassphrase} to {MaxPassphrase} characters.", PassphraseField);
        }
        return passphrase;
    }

    // S3 bucket naming rules: 3-63 lowercase letters, digits, dots and hyphens, starting and ending with a letter or
    // digit, no "..", not an IP address, and none of the reserved prefixes and suffixes.
    private static string Bucket(string? value)
    {
        var bucket = value?.Trim() ?? string.Empty;
        if (bucket.Length == 0)
        {
            throw ApiException.Validation("Enter the bucket name.", BucketField);
        }
        if (!BucketPattern().IsMatch(bucket)
            || bucket.Contains("..", StringComparison.Ordinal)
            || IpPattern().IsMatch(bucket)
            || BucketPrefixes.Any(p => bucket.StartsWith(p, StringComparison.Ordinal))
            || BucketSuffixes.Any(s => bucket.EndsWith(s, StringComparison.Ordinal)))
        {
            throw ApiException.Validation(
                "A bucket name is 3 to 63 lowercase letters, digits, dots and hyphens, and starts and ends with a letter or digit.",
                BucketField);
        }
        return bucket;
    }

    // D10: absolute http or https, with no user info, query or fragment. http and private addresses are allowed.
    private static string? Endpoint(string? value)
    {
        var text = Blank(value);
        if (text is null)
        {
            return null;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Host.Length == 0)
        {
            throw ApiException.Validation("The endpoint must be an absolute http or https address.", EndpointField);
        }
        if (uri.UserInfo.Length > 0 || text.Contains('@', StringComparison.Ordinal))
        {
            throw ApiException.Validation("Put the keys in the key fields, not in the endpoint address.", EndpointField);
        }
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0 || text.Contains('?', StringComparison.Ordinal) || text.Contains('#', StringComparison.Ordinal))
        {
            throw ApiException.Validation("The endpoint address can't have a query or fragment.", EndpointField);
        }
        return text;
    }

    // Amazon S3 itself takes a region from the AWS list; an S3-compatible endpoint takes a free-text signing region.
    private static string Region(string? value, string? endpoint)
    {
        var region = value?.Trim() ?? string.Empty;
        if (endpoint is null)
        {
            return RegionEndpoint.EnumerableAllRegions.Any(r => string.Equals(r.SystemName, region, StringComparison.Ordinal))
                ? region
                : throw ApiException.Validation("Choose an AWS region, or enter the endpoint of an S3-compatible store.", RegionField);
        }
        return SigningRegionPattern().IsMatch(region)
            ? region
            : throw ApiException.Validation("Enter the signing region the endpoint expects (often us-east-1): letters, digits, hyphens and underscores.", RegionField);
    }

    private static string Prefix(string? value)
    {
        var prefix = value?.Trim() ?? string.Empty;
        if (prefix.Length > MaxPrefix || prefix.StartsWith('/') || prefix.Contains("//", StringComparison.Ordinal) || prefix.Any(char.IsControl))
        {
            throw ApiException.Validation($"The prefix is at most {MaxPrefix} characters, doesn't start with / and has no empty folder (//).", PrefixField);
        }
        return prefix.Length == 0 || prefix.EndsWith('/') ? prefix : prefix + "/";
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsOneLine(string value) => value.Length <= MaxKey && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketPattern();

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex IpPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SigningRegionPattern();
}
