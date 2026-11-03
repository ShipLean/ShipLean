namespace ShipLean.Application.Features.Search.Ranking;

public enum RankedListName
{
    TitleLexical,
    BodyLexical,
    TitleVector,
    BodyVector,

    // The any-word lexical list over body, comment and attachment rows, present only when the semantic leg is unavailable. Last, so it loses every tie.
    BodyLexicalAny,

    // Keys that contain the typed part (KeyItemCandidates). After the text lists, so an item that also matched in its text keeps that field and snippet on a tie.
    KeyPart,
}
