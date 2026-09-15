-- Generated from Data/seeds/actor-catalog/native-actor-slot-overrides.json.
-- Do not hand-edit native slot assignments in this migration.
-- source_sha256: 5a190cec9e036edbc3fc2830023a81c6a515c3e195f4de1beccc266fe1cc6f44

-- The guarded statements keep this migration safe for direct-core-only
-- databases that do not contain the normalized repository tables.
SET @has_static_actor_spawns = (
  SELECT COUNT(*)
  FROM information_schema.tables
  WHERE table_schema = DATABASE()
    AND table_name = 'static_actor_spawns'
);

SET @native_slot_alter = IF(
  @has_static_actor_spawns = 1,
  'ALTER TABLE `static_actor_spawns`
     ADD COLUMN IF NOT EXISTS `native_actor_slot` int(10) unsigned NULL AFTER `spawn_id`,
     ADD COLUMN IF NOT EXISTS `native_actor_scope` varchar(128)
       AS (CONCAT(`zone_id`, '':'', COALESCE(`private_area_name`, ''''), '':'', `private_area_level`)) STORED',
  'SELECT 1'
);
PREPARE native_slot_alter_statement FROM @native_slot_alter;
EXECUTE native_slot_alter_statement;
DEALLOCATE PREPARE native_slot_alter_statement;

-- Clear earlier guessed or row-order-derived assignments in this
-- catalog-authoritative scope before applying reviewed slots.
SET @native_slot_clear = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = NULL
WHERE `zone_id` = 155
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0',
  'SELECT 1'
);
PREPARE native_slot_clear_statement FROM @native_slot_clear;
EXECUTE native_slot_clear_statement;
DEALLOCATE PREPARE native_slot_clear_statement;

SET @native_slot_update = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = CASE `spawn_id`
  WHEN 574 THEN 23
  WHEN 575 THEN 51
  WHEN 576 THEN 11
  WHEN 577 THEN 38
  WHEN 578 THEN 9
  WHEN 579 THEN 50
  WHEN 580 THEN 10
  WHEN 581 THEN 44
  WHEN 582 THEN 45
  WHEN 583 THEN 4
  WHEN 584 THEN 49
  WHEN 585 THEN 7
  WHEN 586 THEN 39
  WHEN 587 THEN 8
  WHEN 588 THEN 24
  WHEN 589 THEN 53
  WHEN 590 THEN 54
  WHEN 591 THEN 31
  WHEN 592 THEN 43
  WHEN 593 THEN 42
  WHEN 594 THEN 22
  WHEN 595 THEN 12
  WHEN 596 THEN 14
  WHEN 597 THEN 13
  WHEN 599 THEN 33
  WHEN 600 THEN 35
  WHEN 601 THEN 32
  WHEN 602 THEN 36
  WHEN 603 THEN 15
  WHEN 604 THEN 29
  WHEN 605 THEN 48
  WHEN 606 THEN 16
  WHEN 607 THEN 17
  WHEN 608 THEN 19
  WHEN 609 THEN 18
  WHEN 610 THEN 52
  WHEN 611 THEN 20
  WHEN 612 THEN 21
  WHEN 613 THEN 40
  WHEN 614 THEN 25
  WHEN 615 THEN 34
  WHEN 632 THEN 41
  WHEN 721 THEN 28
  WHEN 722 THEN 27
  WHEN 723 THEN 26
  WHEN 724 THEN 30
  ELSE `native_actor_slot`
END
WHERE `zone_id` = 155
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0
  AND `spawn_id` IN (574,575,576,577,578,579,580,581,582,583,584,585,586,587,588,589,590,591,592,593,594,595,596,597,599,600,601,602,603,604,605,606,607,608,609,610,611,612,613,614,615,632,721,722,723,724)',
  'SELECT 1'
);
PREPARE native_slot_update_statement FROM @native_slot_update;
EXECUTE native_slot_update_statement;
DEALLOCATE PREPARE native_slot_update_statement;

-- Clear earlier guessed or row-order-derived assignments in this
-- catalog-authoritative scope before applying reviewed slots.
SET @native_slot_clear = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = NULL
WHERE `zone_id` = 206
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0',
  'SELECT 1'
);
PREPARE native_slot_clear_statement FROM @native_slot_clear;
EXECUTE native_slot_clear_statement;
DEALLOCATE PREPARE native_slot_clear_statement;

SET @native_slot_update = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = CASE `spawn_id`
  WHEN 564 THEN 183
  WHEN 565 THEN 17
  WHEN 566 THEN 139
  WHEN 567 THEN 134
  WHEN 568 THEN 169
  WHEN 569 THEN 163
  WHEN 570 THEN 170
  WHEN 572 THEN 130
  WHEN 573 THEN 57
  WHEN 621 THEN 189
  WHEN 622 THEN 190
  WHEN 623 THEN 184
  WHEN 624 THEN 185
  WHEN 625 THEN 186
  WHEN 626 THEN 187
  WHEN 631 THEN 39
  WHEN 633 THEN 136
  WHEN 634 THEN 133
  WHEN 635 THEN 135
  WHEN 637 THEN 4
  WHEN 638 THEN 21
  WHEN 639 THEN 127
  WHEN 640 THEN 22
  WHEN 641 THEN 20
  WHEN 642 THEN 11
  WHEN 643 THEN 9
  WHEN 644 THEN 14
  WHEN 645 THEN 125
  WHEN 646 THEN 13
  WHEN 647 THEN 7
  WHEN 648 THEN 15
  WHEN 649 THEN 12
  WHEN 650 THEN 16
  WHEN 651 THEN 19
  WHEN 652 THEN 8
  WHEN 653 THEN 10
  WHEN 654 THEN 18
  WHEN 655 THEN 124
  WHEN 656 THEN 5
  WHEN 657 THEN 128
  WHEN 658 THEN 24
  WHEN 659 THEN 123
  WHEN 660 THEN 23
  WHEN 661 THEN 44
  WHEN 662 THEN 42
  WHEN 663 THEN 32
  WHEN 664 THEN 98
  WHEN 665 THEN 180
  WHEN 666 THEN 181
  WHEN 667 THEN 58
  WHEN 668 THEN 144
  WHEN 669 THEN 60
  WHEN 670 THEN 59
  WHEN 671 THEN 143
  WHEN 672 THEN 147
  WHEN 673 THEN 145
  WHEN 674 THEN 146
  WHEN 675 THEN 38
  WHEN 676 THEN 75
  WHEN 677 THEN 114
  WHEN 678 THEN 142
  WHEN 679 THEN 112
  WHEN 680 THEN 99
  WHEN 681 THEN 113
  WHEN 682 THEN 72
  WHEN 683 THEN 73
  WHEN 684 THEN 69
  WHEN 685 THEN 111
  WHEN 686 THEN 166
  WHEN 687 THEN 121
  WHEN 688 THEN 70
  WHEN 689 THEN 74
  WHEN 690 THEN 138
  WHEN 691 THEN 41
  WHEN 692 THEN 26
  WHEN 693 THEN 25
  WHEN 694 THEN 27
  WHEN 695 THEN 116
  WHEN 696 THEN 71
  WHEN 697 THEN 140
  WHEN 698 THEN 115
  WHEN 699 THEN 31
  WHEN 700 THEN 33
  WHEN 701 THEN 85
  WHEN 702 THEN 49
  WHEN 703 THEN 34
  WHEN 704 THEN 90
  WHEN 705 THEN 50
  WHEN 706 THEN 35
  WHEN 707 THEN 82
  WHEN 708 THEN 81
  WHEN 709 THEN 89
  WHEN 710 THEN 102
  WHEN 711 THEN 83
  WHEN 712 THEN 43
  WHEN 713 THEN 52
  WHEN 714 THEN 84
  WHEN 715 THEN 80
  WHEN 716 THEN 51
  WHEN 717 THEN 101
  WHEN 718 THEN 79
  WHEN 719 THEN 100
  WHEN 720 THEN 141
  ELSE `native_actor_slot`
END
WHERE `zone_id` = 206
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0
  AND `spawn_id` IN (564,565,566,567,568,569,570,572,573,621,622,623,624,625,626,631,633,634,635,637,638,639,640,641,642,643,644,645,646,647,648,649,650,651,652,653,654,655,656,657,658,659,660,661,662,663,664,665,666,667,668,669,670,671,672,673,674,675,676,677,678,679,680,681,682,683,684,685,686,687,688,689,690,691,692,693,694,695,696,697,698,699,700,701,702,703,704,705,706,707,708,709,710,711,712,713,714,715,716,717,718,719,720)',
  'SELECT 1'
);
PREPARE native_slot_update_statement FROM @native_slot_update;
EXECUTE native_slot_update_statement;
DEALLOCATE PREPARE native_slot_update_statement;

-- Clear earlier guessed or row-order-derived assignments in this
-- catalog-authoritative scope before applying reviewed slots.
SET @native_slot_clear = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = NULL
WHERE `zone_id` = 244
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0',
  'SELECT 1'
);
PREPARE native_slot_clear_statement FROM @native_slot_clear;
EXECUTE native_slot_clear_statement;
DEALLOCATE PREPARE native_slot_clear_statement;

SET @native_slot_update = IF(
  @has_static_actor_spawns = 1,
  'UPDATE `static_actor_spawns`
SET `native_actor_slot` = CASE `spawn_id`
  WHEN 536 THEN 6
  WHEN 537 THEN 7
  WHEN 538 THEN 15
  WHEN 539 THEN 12
  WHEN 930 THEN 10
  ELSE `native_actor_slot`
END
WHERE `zone_id` = 244
  AND COALESCE(`private_area_name`, '''') = ''''
  AND `private_area_level` = 0
  AND `spawn_id` IN (536,537,538,539,930)',
  'SELECT 1'
);
PREPARE native_slot_update_statement FROM @native_slot_update;
EXECUTE native_slot_update_statement;
DEALLOCATE PREPARE native_slot_update_statement;

SET @native_slot_index = IF(
  @has_static_actor_spawns = 1
    AND NOT EXISTS (
      SELECT 1
      FROM information_schema.statistics
      WHERE table_schema = DATABASE()
        AND table_name = 'static_actor_spawns'
        AND index_name = 'uq_static_actor_native_slot'
    ),
  'ALTER TABLE `static_actor_spawns`
     ADD UNIQUE KEY `uq_static_actor_native_slot` (`native_actor_scope`,`native_actor_slot`)',
  'SELECT 1'
);
PREPARE native_slot_index_statement FROM @native_slot_index;
EXECUTE native_slot_index_statement;
DEALLOCATE PREPARE native_slot_index_statement;
