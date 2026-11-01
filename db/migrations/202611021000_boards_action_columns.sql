-- Boards that were saved before the Action item type gain its mappings (a column must map exactly one state of each type the
-- board shows). On a stories board, the k-th column of a category gets the k-th Action state of that category (the rule
-- BoardDefaultSettings.MapOther uses for a second type); a column left without one repeats the nearest earlier column's Action state,
-- or the first live Action state (BoardDefaultSettings.FillGaps). A column's category is its Story state's. Stories and Taskboard
-- boards also gain an Action card-field setting copied from the Story one (Taskboard: the Bug one). A board that already has an
-- Action mapping or card setting is left alone, so running the script again changes nothing. Nothing is deleted.
DO $$
DECLARE
    board_row record;
    column_row record;
    action_states uuid[];
    category_count jsonb;
    seen int;
    story_category text;
    picked uuid;
    previous uuid;
    columns jsonb;
BEGIN
    FOR board_row IN
        SELECT b.id, b.project_id, b.settings FROM board b
        WHERE b.level = 'stories'
          AND jsonb_array_length(b.settings -> 'columns') > 0
          AND NOT EXISTS (SELECT 1 FROM jsonb_array_elements(b.settings -> 'columns') col, jsonb_array_elements(col -> 'stateMappings') m WHERE m ->> 'type' = 'action')
    LOOP
        SELECT id INTO previous FROM state
        WHERE project_id = board_row.project_id AND type = 'action' AND category <> 'removed'
        ORDER BY CASE category WHEN 'proposed' THEN 0 WHEN 'inProgress' THEN 1 WHEN 'resolved' THEN 2 ELSE 3 END, sort_order LIMIT 1;
        IF previous IS NULL THEN
            CONTINUE;
        END IF;
        columns := '[]'::jsonb;
        category_count := '{}'::jsonb;
        FOR column_row IN SELECT col, ord FROM jsonb_array_elements(board_row.settings -> 'columns') WITH ORDINALITY AS e (col, ord) ORDER BY ord
        LOOP
            SELECT s.category INTO story_category
            FROM jsonb_array_elements(column_row.col -> 'stateMappings') m JOIN state s ON s.id = (m ->> 'stateId')::uuid
            WHERE m ->> 'type' = 'story' LIMIT 1;
            seen := COALESCE((category_count ->> story_category)::int, 0);
            category_count := category_count || jsonb_build_object(COALESCE(story_category, ''), seen + 1);
            SELECT id INTO picked FROM state
            WHERE project_id = board_row.project_id AND type = 'action' AND category = story_category
            ORDER BY sort_order OFFSET seen LIMIT 1;
            previous := COALESCE(picked, previous);
            columns := columns || jsonb_build_array(jsonb_set(
                column_row.col, '{stateMappings}',
                (column_row.col -> 'stateMappings') || jsonb_build_array(jsonb_build_object('type', 'action', 'stateId', previous))));
        END LOOP;
        UPDATE board SET settings = jsonb_set(settings, '{columns}', columns) WHERE id = board_row.id;
    END LOOP;
END $$;

UPDATE board b
SET settings = jsonb_set(b.settings, '{cardFields}', (b.settings -> 'cardFields') || jsonb_build_array(
    (SELECT f || jsonb_build_object('type', 'action') FROM jsonb_array_elements(b.settings -> 'cardFields') f
     WHERE f ->> 'type' = CASE WHEN b.level = 'stories' THEN 'story' ELSE 'bug' END LIMIT 1)))
WHERE b.level IN ('stories', 'taskboard')
  AND NOT EXISTS (SELECT 1 FROM jsonb_array_elements(b.settings -> 'cardFields') f WHERE f ->> 'type' = 'action')
  AND EXISTS (SELECT 1 FROM jsonb_array_elements(b.settings -> 'cardFields') f WHERE f ->> 'type' = CASE WHEN b.level = 'stories' THEN 'story' ELSE 'bug' END);
