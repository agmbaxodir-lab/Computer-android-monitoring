CREATE TABLE users (
  id uuid PRIMARY KEY, username text NOT NULL UNIQUE, password_hash text NOT NULL,
  role text NOT NULL CHECK (role IN ('Admin','User')), is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE refresh_tokens (
  id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash text NOT NULL UNIQUE, expires_at timestamptz NOT NULL, revoked_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE devices (
  id uuid PRIMARY KEY, hostname text NOT NULL, username text, ip_address text, os_version text,
  agent_version text, secret_hash bytea NOT NULL, status text NOT NULL DEFAULT 'Active',
  last_heartbeat_at timestamptz, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE applications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name text NOT NULL UNIQUE,
  process_names text[] NOT NULL, enabled boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE file_events (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), event_id uuid NOT NULL UNIQUE,
  device_id uuid NOT NULL REFERENCES devices(id), user_id uuid REFERENCES users(id),
  application_id uuid REFERENCES applications(id), event_type text NOT NULL,
  process_name text, os_username text, file_name text NOT NULL, file_extension text,
  mime_type text, file_size bigint NOT NULL, sha256 char(64), timestamp timestamptz NOT NULL,
  confidence real NOT NULL CHECK (confidence BETWEEN 0 AND 1), status text NOT NULL DEFAULT 'New',
  created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX ix_fe_ts ON file_events(timestamp DESC);
CREATE INDEX ix_fe_device_ts ON file_events(device_id, timestamp DESC);
CREATE INDEX ix_fe_app ON file_events(application_id);
CREATE INDEX ix_fe_ext ON file_events(file_extension);
CREATE INDEX ix_fe_sha ON file_events(sha256);
CREATE TABLE policies (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name text NOT NULL, enabled boolean NOT NULL DEFAULT true,
  action text NOT NULL DEFAULT 'Alert', created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE policy_rules (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), policy_id uuid NOT NULL REFERENCES policies(id) ON DELETE CASCADE,
  field text NOT NULL, operator text NOT NULL, value text NOT NULL);
CREATE TABLE alerts (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), file_event_id uuid REFERENCES file_events(id),
  policy_id uuid REFERENCES policies(id), severity text NOT NULL DEFAULT 'Medium',
  message text NOT NULL, status text NOT NULL DEFAULT 'Open', created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX ix_alerts_created ON alerts(created_at DESC);
CREATE TABLE notifications (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(), target_type text NOT NULL CHECK (target_type IN ('Device','User','All')),
  target_id uuid, title text NOT NULL, message text NOT NULL, status text NOT NULL DEFAULT 'Pending',
  created_by uuid REFERENCES users(id), created_at timestamptz NOT NULL DEFAULT now(), delivered_at timestamptz);
CREATE INDEX ix_notif_pending ON notifications(status) WHERE status = 'Pending';
CREATE TABLE audit_logs (
  id bigserial PRIMARY KEY, actor text NOT NULL, action text NOT NULL, entity text, entity_id text,
  details jsonb, ip_address text, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE agent_heartbeats (
  id bigserial PRIMARY KEY, device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  agent_version text, ip_address text, cpu_percent real, memory_mb real, queue_size int,
  created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX ix_hb_device ON agent_heartbeats(device_id, created_at DESC);

INSERT INTO applications(name, process_names) VALUES
 ('Telegram','{Telegram.exe}'),('WhatsApp','{WhatsApp.exe,WhatsApp.Root.exe}'),
 ('imo','{imo.exe}'),('Microsoft Teams','{ms-teams.exe,Teams.exe}'),('Discord','{Discord.exe}');
