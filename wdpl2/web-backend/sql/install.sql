-- =====================================================================
-- WDPL Web Inbox — one-shot install script
-- ---------------------------------------------------------------------
-- Import this file directly into your existing (empty) database via
-- phpMyAdmin:
--   1. phpMyAdmin → click your database in the left sidebar
--   2. Click the "Import" tab at the top
--   3. Choose File → select this file (install.sql)
--   4. Scroll down, click "Import"
--
-- Safe to re-run: uses CREATE TABLE IF NOT EXISTS, so it won't wipe
-- existing data. It will NOT drop or alter tables you already have.
-- =====================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ---------------------------------------------------------------------
-- Table 1: submissions
-- Every form post from the website lands here until the MAUI app
-- marks it processed.
-- ---------------------------------------------------------------------
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

-- ---------------------------------------------------------------------
-- Table 2: captain_tokens
-- One row per captain. The token is the shared secret they paste into
-- the website form to prove they're allowed to submit match results.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS captain_tokens (
  token        CHAR(40)     NOT NULL PRIMARY KEY,   -- random 40-char hex string
  captain_name VARCHAR(120) NOT NULL,
  team_id      CHAR(36)     NULL,
  enabled      TINYINT(1)   NOT NULL DEFAULT 1,
  created_utc  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  last_used    DATETIME     NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------------------------------------------------------------------
-- Seed: a test captain token so you can immediately try the sample
-- form (examples/result-form.html). Delete this row once you've added
-- real captain tokens.
-- ---------------------------------------------------------------------
INSERT IGNORE INTO captain_tokens (token, captain_name, enabled)
VALUES ('test0000000000000000000000000000000000aa', 'Test Captain', 1);

SET FOREIGN_KEY_CHECKS = 1;

-- Done. Verify in phpMyAdmin that two tables now exist:
--   submissions, captain_tokens
