/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * Umbra framework releases, official repositories, developer resources, and
 * safety blocks are owned by the separate signed Dev Unit update origin.
 * Public AetherXIV server installations must not host or administer them.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

DROP TABLE IF EXISTS `launcher_umbra_plugins`;
DROP TABLE IF EXISTS `launcher_umbra_plugin_blocks`;
DROP TABLE IF EXISTS `launcher_umbra_plugin_repositories`;
DROP TABLE IF EXISTS `launcher_umbra_framework_artifacts`;
DROP TABLE IF EXISTS `launcher_config_plugin_catalogs`;
DROP TABLE IF EXISTS `launcher_umbra_plugin_releases_v13`;
DROP TABLE IF EXISTS `launcher_umbra_plugin_blocks_v13`;
DROP TABLE IF EXISTS `launcher_umbra_plugin_repositories_v13`;
DROP TABLE IF EXISTS `launcher_umbra_framework_artifacts_v13`;

UPDATE `launcher_config`
SET `client_plugin_framework_catalog_url` = NULL,
    `plugin_blocklist_url` = NULL;
