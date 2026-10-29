-- Board settings gain a "transitions" section (Stories and Features boards use it; the others keep it empty).
UPDATE board SET settings = jsonb_set(settings, '{transitions}', '[]'::jsonb) WHERE NOT (settings ? 'transitions');
