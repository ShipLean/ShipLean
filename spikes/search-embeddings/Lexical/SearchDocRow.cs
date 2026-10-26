namespace SearchEmbeddings.Lexical;

// One search_doc-like row: Text carries the item-title prefix line for body and comment rows, as production does.
public sealed record SearchDocRow(string ItemRef, string Field, int ChunkNo, string Text);
