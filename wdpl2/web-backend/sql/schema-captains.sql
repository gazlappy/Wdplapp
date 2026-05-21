-- WDPL Captain Portal schema additions.
-- Run this once in phpMyAdmin against your inbox database (after schema.sql).

-- Captain accounts. One login per team.
CREATE TABLE IF NOT EXISTS captains (
  team_id        CHAR(36)     NOT NULL PRIMARY KEY,
  team_name      VARCHAR(120) NOT NULL,
  division_id    CHAR(36)     NULL,
  division_name  VARCHAR(120) NULL,
  username       VARCHAR(80)  NOT NULL UNIQUE,
  password_hash  VARCHAR(255) NOT NULL,    -- password_hash(PASSWORD_DEFAULT)
  display_name   VARCHAR(120) NULL,
  email          VARCHAR(160) NULL,
  enabled        TINYINT(1)   NOT NULL DEFAULT 1,
  created_utc    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_login     DATETIME     NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Snapshot of the league teams (pushed by the MAUI app).
CREATE TABLE IF NOT EXISTS league_teams (
  team_id        CHAR(36)     NOT NULL PRIMARY KEY,
  season_id      CHAR(36)     NULL,
  division_id    CHAR(36)     NULL,
  name           VARCHAR(120) NOT NULL,
  division_name  VARCHAR(120) NULL,
  venue_name     VARCHAR(160) NULL,
  updated_utc    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Snapshot of league players (pushed by the MAUI app).
CREATE TABLE IF NOT EXISTS league_players (
  player_id         CHAR(36)     NOT NULL PRIMARY KEY,
  team_id           CHAR(36)     NULL,
  season_id         CHAR(36)     NULL,
  full_name         VARCHAR(160) NOT NULL,
  is_active         TINYINT(1)   NOT NULL DEFAULT 1,
  added_by_captain  TINYINT(1)   NOT NULL DEFAULT 0,
  updated_utc       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_team (team_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Snapshot of league fixtures (pushed by the MAUI app).
CREATE TABLE IF NOT EXISTS league_fixtures (
  fixture_id     CHAR(36)     NOT NULL PRIMARY KEY,
  season_id      CHAR(36)     NULL,
  division_id    CHAR(36)     NULL,
  home_team_id   CHAR(36)     NOT NULL,
  away_team_id   CHAR(36)     NOT NULL,
  home_team_name VARCHAR(120) NULL,
  away_team_name VARCHAR(120) NULL,
  venue_name     VARCHAR(160) NULL,
  fixture_date   DATETIME     NOT NULL,
  updated_utc    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_home (home_team_id, fixture_date),
  INDEX ix_away (away_team_id, fixture_date),
  INDEX ix_date (fixture_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Lightweight session table (cookie-based login).
CREATE TABLE IF NOT EXISTS captain_sessions (
  token        CHAR(64)  NOT NULL PRIMARY KEY,
  team_id      CHAR(36)  NOT NULL,
  created_utc  DATETIME  NOT NULL DEFAULT CURRENT_TIMESTAMP,
  expires_utc  DATETIME  NOT NULL,
  INDEX ix_expires (expires_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Player availability tracking (per week).
-- week_start_utc is Monday 00:00 of the ISO week (same as me.php fixture window).
CREATE TABLE IF NOT EXISTS player_availability (
  player_id       CHAR(36) NOT NULL,
  week_start_utc  DATETIME NOT NULL,
  available       TINYINT(1) NOT NULL DEFAULT 1,
  updated_utc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (player_id, week_start_utc),
  INDEX ix_week (week_start_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Captain-to-captain (and admin-to-captain) messaging.
-- from_team_id = NULL means the admin/system sent it.
-- to_team_id   = NULL means it's a broadcast to every captain.
CREATE TABLE IF NOT EXISTS captain_messages (
  message_id    BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
  from_team_id  CHAR(36)     NULL,
  to_team_id    CHAR(36)     NULL,
  subject       VARCHAR(160) NOT NULL,
  body          TEXT         NOT NULL,
  sent_utc      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_to (to_team_id, sent_utc),
  INDEX ix_from (from_team_id, sent_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Per-recipient read tracking (supports broadcast messages).
CREATE TABLE IF NOT EXISTS captain_message_reads (
  message_id   BIGINT   NOT NULL,
  team_id      CHAR(36) NOT NULL,
  read_utc     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (message_id, team_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
