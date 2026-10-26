-- The install and its people. Every user is seeded without an avatar photo (initials on the colour).
-- Dev passwords are in SeedIds.Passwords; the hashes are ASP.NET Core Identity PasswordHasher v3.
-- Instance admins: Joel and Tom. Ana must change her password at first sign-in.

INSERT INTO instance_settings (name, base_url, project_creation)
VALUES ('Larkspur Engineering', 'https://shiplean.larkspur.dev', 'everyone');

INSERT INTO app_user (id, email, name, initials, avatar_colour, password_hash, must_change_password, security_stamp, is_admin, created_at)
VALUES
  ('0199f000-0000-7000-8000-000000000001', 'joel@shiplean.local', 'Joel Dickson', 'JD', '#5b6b8c', 'AQAAAAIAAYagAAAAEPWvUBAibe538jLuIXqMir8XedF1NYVubTgswHDu3U84mz7aj6FQScuwFLz3iSEMGA==', false, 'seed-stamp-joel', true, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000002', 'priya@shiplean.local', 'Priya Shah', 'PS', '#2f7fd8', 'AQAAAAIAAYagAAAAENgE8sAs9BuM5KkPp0kZLv32JGv0l4ihdl2/37c0ZTXpf6+bBKEgDnv94qTjszuusw==', false, 'seed-stamp-priya', false, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000003', 'tom@shiplean.local', 'Tom Okafor', 'TO', '#16a089', 'AQAAAAIAAYagAAAAELWT6ECjMWzVhlE6KBJIlpj+Jy+L2yPa4CnXvZxtwZ1XUa4+PrDb4iRNADXEuG0hyw==', false, 'seed-stamp-tom', true, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000004', 'mei@shiplean.local', 'Mei Lin', 'ML', '#7b4fc9', 'AQAAAAIAAYagAAAAEEBbCT681ALJ1mY35akXQo4gS3TuTFKvQTDEug8Ej2hqKl8GDRTAzCF5sl79A40GbQ==', false, 'seed-stamp-mei', false, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000005', 'sam@shiplean.local', 'Sam Rivera', 'SR', '#c9611a', 'AQAAAAIAAYagAAAAEKOdKy7qBNgILh50ScPo7Cv282+ev9RE2ZkaJ4KAl3VIBdGYxp7hpyv5s+j1cVUE/g==', false, 'seed-stamp-sam', false, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc'),
  ('00000001-0000-4000-8000-000000000006', 'ana@shiplean.local', 'Ana Costa', 'AC', '#b83d7a', 'AQAAAAIAAYagAAAAENxyUMwynEokwXo2ijdVOEAfsvqXzk0NQiyqUnBCUWaoV1NYsD7pX4FfXIhKWf8SaA==', true, 'seed-stamp-ana', false, ((now() AT TIME ZONE 'utc')::date - 90)::timestamp AT TIME ZONE 'utc');
