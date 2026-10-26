using Agoda.IoC.Core;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Core;

namespace ShipLean.Application.Features.Boards.View;

// The extra fields a board's settings ask a card to show, formatted by the server. Fields the card already draws (state, parent,
// assignee, points, tags) are left out even when the settings list them, and so is a field that does not apply to the card's type.
[RegisterPerRequest]
public sealed class CardExtraFields(TimeProvider clock)
{
    private static readonly string[] AlreadyDrawn = [FieldIds.State, FieldIds.Parent, FieldIds.Assignee, FieldIds.Points, FieldIds.Tags];

    public IReadOnlyList<CardFieldDto> For(BoardCardRow card, WorkItemTypeId type, IReadOnlyList<CardFieldSettingJson> settings)
    {
        var setting = settings.FirstOrDefault(s => s.Type == type);
        if (setting is null)
        {
            return [];
        }

        var fields = new List<CardFieldDto>();
        foreach (var id in setting.ExtraFields.Where(f => !AlreadyDrawn.Contains(f)))
        {
            var definition = FieldIds.All.FirstOrDefault(f => f.Id == id);
            if (definition is null || !definition.AppliesTo.Contains(type))
            {
                continue;
            }

            var text = Text(definition, card);
            if (text is not null || setting.ShowEmptyFields)
            {
                fields.Add(new CardFieldDto(id, definition.Label, text));
            }
        }

        return fields;
    }

    private string? Text(FieldDefinition definition, BoardCardRow card) => definition.Id switch
    {
        FieldIds.Priority => card.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FieldIds.Severity => card.Severity is { } severity ? OptionLabel(definition, severity) : null,
        FieldIds.Sprint => card.SprintName,
        FieldIds.CreatedAt => DisplayDate.Relative(card.CreatedAt, clock),
        FieldIds.CreatedBy => card.CreatedByName,
        FieldIds.ChangedAt => DisplayDate.Relative(card.ChangedAt, clock),
        FieldIds.ChangedBy => card.ChangedByName,
        _ => null,
    };

    private static string OptionLabel(FieldDefinition definition, int value)
        => definition.Values?.FirstOrDefault(v => v.Value.TryGetInt32(out var option) && option == value)?.Label
            ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
