using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Boards.View;

// A board card as the criteria evaluator sees it: its field values by field id, in the JSON shapes of the field kind.
public sealed class CardFieldValues(BoardCardRow card, Guid? epicId) : IFieldValues
{
    public JsonElement Get(string fieldId) => fieldId switch
    {
        FieldIds.Id => Text(card.Id.ToString()),
        FieldIds.Key => Text(card.Key),
        FieldIds.Title => Text(card.Title),
        FieldIds.Type => Text(card.Type),
        FieldIds.State => Text(card.StateId.ToString()),
        FieldIds.StateCategory => Text(card.StateCategory),
        FieldIds.Assignee => Guid(card.AssigneeId),
        FieldIds.Priority => Number(card.Priority),
        FieldIds.Points => card.Points is { } points ? Number(points) : Null,
        FieldIds.Tags => JsonSerializer.SerializeToElement(card.TagNames),
        FieldIds.Sprint => Guid(card.SprintId),
        FieldIds.Parent => Guid(card.ParentId),
        FieldIds.Epic => Guid(epicId),
        FieldIds.Severity => card.Severity is { } severity ? Number(severity) : Null,
        FieldIds.CreatedAt => Text(card.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
        FieldIds.CreatedBy => Guid(card.CreatedById),
        FieldIds.ChangedAt => Text(card.ChangedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
        FieldIds.ChangedBy => Guid(card.ChangedById),
        FieldIds.OriginalSystem => card.OriginalSystem is { } system ? Text(system) : Null,
        FieldIds.OriginalSystemId => card.OriginalSystemId is { } systemId ? Text(systemId) : Null,
        _ => default,
    };

    private static JsonElement Null => JsonSerializer.SerializeToElement<string?>(null);

    private static JsonElement Text(string value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Number(decimal value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Guid(Guid? value) => value is { } id ? Text(id.ToString()) : Null;
}
