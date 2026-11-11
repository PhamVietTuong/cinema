-- e2e prerequisite: the booking spec needs at least one upcoming showtime, but insert_db.sql generates its grid
-- relative to the day it was authored. This moves every showtime that has already started to the same time of day
-- tomorrow, keeping its movie, rooms and duration. Idempotent: after one run nothing is in the past, so a second
-- run changes nothing. Use on a throw-away e2e database only (it rewrites showtime history).
-- Run: sqlcmd -C -I -b -S <server> -d Cinema -f 65001 -i e2e/sql/seed-future-showtime.sql
SET NOCOUNT ON;

DECLARE @now      datetime = GETDATE();
DECLARE @tomorrow date     = CAST(DATEADD(day, 1, @now) AS date);

UPDATE [ShowTime]
SET StartTime = DATEADD(day, DATEDIFF(day, CAST(StartTime AS date), @tomorrow), StartTime),
    EndTime   = DATEADD(day, DATEDIFF(day, CAST(StartTime AS date), @tomorrow), EndTime),
    LastUpdatedTime = GETUTCDATE()
WHERE IsActive = 1
  AND StartTime <= @now;

SELECT COUNT(*) AS UpcomingShowTimes FROM [ShowTime] WHERE IsActive = 1 AND StartTime > @now;
