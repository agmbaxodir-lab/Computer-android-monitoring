-- Migration 003: preserve transfer source/destination metadata for file copy/upload events.
ALTER TABLE file_events
  ADD COLUMN IF NOT EXISTS source text,
  ADD COLUMN IF NOT EXISTS destination text;

CREATE INDEX IF NOT EXISTS ix_fe_source ON file_events(source);
CREATE INDEX IF NOT EXISTS ix_fe_destination ON file_events(destination);

-- Windows processes used for browser file transfers and Explorer/USB/network copies.
INSERT INTO applications(name, process_names) VALUES
  ('Google Chrome', '{chrome.exe}'),
  ('Microsoft Edge', '{msedge.exe}'),
  ('Mozilla Firefox', '{firefox.exe}'),
  ('Brave', '{brave.exe}'),
  ('Opera', '{opera.exe}')
ON CONFLICT (name) DO NOTHING;
