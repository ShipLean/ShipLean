using System.Text.Json;

namespace ShipLean.Contracts.Criteria;

// A work item's field values by field id (FieldIds), in the JSON shapes of the field kind: JSON null when empty,
// an array for tags. Many implementations: a snapshot, a DTO, a database row.
[MultipleImplementations]
public interface IFieldValues
{
    JsonElement Get(string fieldId);
}
