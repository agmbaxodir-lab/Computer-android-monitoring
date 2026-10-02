-- Migration 002: Add platform, device_model, and file_path support for Android and multi-platform agents

ALTER TABLE devices 
  ADD COLUMN IF NOT EXISTS platform text NOT NULL DEFAULT 'Windows',
  ADD COLUMN IF NOT EXISTS device_model text;

CREATE INDEX IF NOT EXISTS ix_devices_platform ON devices(platform);

ALTER TABLE file_events 
  ADD COLUMN IF NOT EXISTS platform text NOT NULL DEFAULT 'Windows',
  ADD COLUMN IF NOT EXISTS file_path text;

CREATE INDEX IF NOT EXISTS ix_fe_platform ON file_events(platform);
CREATE INDEX IF NOT EXISTS ix_fe_event_type ON file_events(event_type);

-- Android package identifiers for default monitored apps
INSERT INTO applications(name, process_names) VALUES
  ('Telegram (Android)', '{org.telegram.messenger,org.telegram.messenger.web}'),
  ('WhatsApp (Android)', '{com.whatsapp,com.whatsapp.w4b}'),
  ('imo (Android)', '{com.imo.android.imoim,com.imo.android.imoimbeta}'),
  ('Microsoft Teams (Android)', '{com.microsoft.teams}'),
  ('Discord (Android)', '{com.discord}')
ON CONFLICT (name) DO NOTHING;
