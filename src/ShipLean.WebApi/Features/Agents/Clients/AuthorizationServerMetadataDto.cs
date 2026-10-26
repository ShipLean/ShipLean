using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Agents.Clients;

// RFC 8414 authorization server metadata. The names are fixed by the RFC, so they are spelled out.
public sealed record AuthorizationServerMetadataDto(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("revocation_endpoint")] string RevocationEndpoint,
    [property: JsonPropertyName("registration_endpoint")] string RegistrationEndpoint,
    [property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported,
    [property: JsonPropertyName("grant_types_supported")] IReadOnlyList<string> GrantTypesSupported,
    [property: JsonPropertyName("code_challenge_methods_supported")] IReadOnlyList<string> CodeChallengeMethodsSupported,
    [property: JsonPropertyName("token_endpoint_auth_methods_supported")] IReadOnlyList<string> TokenEndpointAuthMethodsSupported,
    [property: JsonPropertyName("client_id_metadata_document_supported")] bool ClientIdMetadataDocumentSupported,
    [property: JsonPropertyName("authorization_response_iss_parameter_supported")] bool AuthorizationResponseIssParameterSupported);
