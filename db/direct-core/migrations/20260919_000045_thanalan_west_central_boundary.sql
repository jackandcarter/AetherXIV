-- Provisional, user-authorized Horizon <-> Black Brush road boundary.
-- NOT recovered retail boxes. Reference: docs/THANALAN_BOUNDARY_2026-09-18.md.
-- Region/zone identities are existing canonical data. X/Z corridor is derived
-- from the user's two logged traversals and nominated point (-615.692,-433.447).
-- Y is deliberately absent: the existing seamless policy operates on X/Z.
-- Preserve any locally implemented link, including reversed orientation.
INSERT INTO server_seamless_zonechange_bounds
 (regionId,zoneId1,zoneId2,
  zone1_boundingbox_x1,zone1_boundingbox_y1,zone1_boundingbox_x2,zone1_boundingbox_y2,
  zone2_boundingbox_x1,zone2_boundingbox_y1,zone2_boundingbox_x2,zone2_boundingbox_y2,
  merge_boundingbox_x1,merge_boundingbox_y1,merge_boundingbox_x2,merge_boundingbox_y2)
SELECT 104,172,170,
 -650,-465,-640,-375,
 -590,-465,-580,-375,
 -630,-465,-600,-375
WHERE NOT EXISTS (
 SELECT 1 FROM server_seamless_zonechange_bounds
 WHERE (zoneId1=172 AND zoneId2=170) OR (zoneId1=170 AND zoneId2=172)
);
