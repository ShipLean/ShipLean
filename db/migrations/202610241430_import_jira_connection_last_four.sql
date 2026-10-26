-- The last four characters of a Jira token, stored when it is saved so that reading a connection never decrypts the token. Null for rows saved before this column and for tokens too short to show.
ALTER TABLE jira_connection ADD COLUMN token_last_four text;
