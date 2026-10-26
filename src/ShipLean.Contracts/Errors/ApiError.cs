using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Errors;

// The one error body every endpoint returns, MVC or minimal API: { "error": "...", "code": "..." }.
// `error` is a sentence the UI can show; `code` is one of ApiErrorCodes. `field` names the request field at fault
// (ApiException.Field) and is left out of the JSON when there is none.
public sealed record ApiError(string Error, string Code, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Field = null);
