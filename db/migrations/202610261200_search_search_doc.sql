-- search_doc (#35): one row per searchable chunk of an item, comment, attachment, project or sprint. It is derived data, rebuilt
-- from those rows by the search indexer (docs/search.md), so it is the one table the app may delete from.
-- tsv is stored (GIN cannot index a virtual generated column). It reads the text with the english configuration for stemming
-- and the simple one so keys (SHIP-12) and identifiers match exactly. The weight comes from the field.
-- For body, comment and attachment rows the first line of text is the item title (docs/search.md, Chunking): it is in the
-- embedding but not in tsv, so a title match is not also a body match. substr(.., strpos(..) + 1) is the whole text when there is no newline.
-- Only title, body, comment, attachment and sprint_goal are embedded (docs/search.md); the partial index covers just those.
-- embedding is the normalised vector of "passage: " + text, null until embedded; embedding_model is name@revision of the writer.
CREATE TABLE search_doc (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project (id),
    source_kind     text NOT NULL CHECK (source_kind IN ('item', 'comment', 'attachment', 'project', 'sprint')),
    source_id       uuid NOT NULL,
    work_item_id    uuid REFERENCES work_item (id),
    chunk_no        integer NOT NULL CHECK (chunk_no >= 0),
    field           text NOT NULL CHECK (field IN ('title', 'tags', 'body', 'comment', 'attachment', 'project', 'sprint_goal', 'sprint_name')),
    text            text NOT NULL,
    text_hash       text NOT NULL,
    tsv             tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english'::regconfig, CASE WHEN field IN ('body', 'comment', 'attachment') THEN substr(text, strpos(text, E'\n') + 1) ELSE text END),
            CASE field
                WHEN 'title' THEN 'A'::"char" WHEN 'project' THEN 'A'::"char"
                WHEN 'tags' THEN 'B'::"char"
                WHEN 'body' THEN 'C'::"char" WHEN 'sprint_goal' THEN 'C'::"char" WHEN 'attachment' THEN 'C'::"char"
                ELSE 'D'::"char"
            END)
        || setweight(to_tsvector('simple'::regconfig, CASE WHEN field IN ('body', 'comment', 'attachment') THEN substr(text, strpos(text, E'\n') + 1) ELSE text END),
            CASE field
                WHEN 'title' THEN 'A'::"char" WHEN 'project' THEN 'A'::"char"
                WHEN 'tags' THEN 'B'::"char"
                WHEN 'body' THEN 'C'::"char" WHEN 'sprint_goal' THEN 'C'::"char" WHEN 'attachment' THEN 'C'::"char"
                ELSE 'D'::"char"
            END)
    ) STORED,
    embedding       real[],
    embedding_model text,
    indexed_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (source_kind, source_id, field, chunk_no)
);
CREATE INDEX ix_search_doc_tsv ON search_doc USING gin (tsv);
CREATE INDEX ix_search_doc_project_indexed ON search_doc (project_id, indexed_at);
CREATE INDEX ix_search_doc_work_item ON search_doc (work_item_id);
CREATE INDEX ix_search_doc_unembedded ON search_doc (project_id) WHERE embedding IS NULL AND field IN ('title', 'body', 'comment', 'attachment', 'sprint_goal');
