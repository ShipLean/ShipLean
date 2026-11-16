interface RailKeyButtonProps {
  itemKey: string;
  onSelect(key: string): void;
  label?: string;
}

/** A key that selects that item on the chart. */
export function RailKeyButton({ itemKey, onSelect, label }: RailKeyButtonProps) {
  return (
    <button
      type="button"
      data-key={itemKey}
      onClick={() => onSelect(itemKey)}
      className="cursor-pointer border-0 bg-transparent p-0 text-left text-signal hover:underline"
    >
      {label ?? itemKey}
    </button>
  );
}
