-- WDPL Web Inbox schema
-- Run this once in phpMyAdmin against your inbox database.

CREATE TABLE IF NOT EXISTS submissions (
  id            BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  type          VARCHAR(40)  NOT NULL,            -- 'match_result' | 'availability' | 'entry' | 'generic'
  season_id     CHAR(36)     NULL,                -- Guid string (optional)
  reference_id  CHAR(36)     NULL,                -- Guid of fixture/match/competition (optional)
  payload_json  MEDIUMTEXT   NOT NULL,            -- raw form data as JSON
  submitter     VARCHAR(120) NULL,                -- captured name / email
  submitter_ip  VARCHAR(45)  NULL,
  received_utc  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  processed     TINYINT(1)   NOT NULL DEFAULT 0,
  processed_utc DATETIME     NULL,
  processed_by  VARCHAR(120) NULL,
  notes         VARCHAR(500) NULL,
  INDEX ix_processed (processed, received_utc),
  INDEX ix_type      (type, processed)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS captain_tokens (
  token        CHAR(40)     NOT NULL PRIMARY KEY,   -- random hex, given out to each captain
  captain_name VARCHAR(120) NOT NULL,
  team_id      CHAR(36)     NULL,
  enabled      TINYINT(1)   NOT NULL DEFAULT 1,
  created_utc  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_used    DATETIME     NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ------------------------------------------------------------------
-- OPTIONAL: insert a test captain token so you can test the form.
-- Generate a real token with any random 40-char hex string later.
-- ------------------------------------------------------------------
-- INSERT INTO captain_tokens (token, captain_name)
-- VALUES ('test0000000000000000000000000000000000aa', 'Test Captain');
