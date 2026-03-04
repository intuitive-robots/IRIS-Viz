# Changelog

All notable changes to this project are documented in this file.
This file is generated from git history.

## v2.0.5 - 2026-03-04

### Changed
- Refactored IRISNode: Removed IRISAnchor and added IRISOrigin for scene offset management

## v2.0.4 - 2026-03-04

### Added
- Axis visualization to IRISNode

### Changed
- Refactored SimSceneLoader prefab: cleaned up unused properties and reset children

## v2.0.3 - 2026-03-04

### Added
- `TrajectorySpawner` for B-spline trajectory visualization with services:
  - `SpawnTrajectory` - creates trajectory from waypoints
  - `UpdateTrajectory` - updates existing trajectory
  - `DeleteTrajectory` - removes trajectory by name
- `TrajectoryWaypoint` class with per-point position and color
- `TrajectoryConfig` MessagePack data class for trajectory configuration
- B-spline interpolation for smooth curve rendering
- Per-point color gradient interpolation for trajectories

### Changed
- Enhanced ServiceManager error handling
- Updated IRISNode prefab for improved functionality

## v2.0.2 - 2026-03-03

### Changed
- Enhance node settings and naming conventions in IRISXRNode

## v2.0.1 - 2026-01-27

### Changed
- Switch to lightweight heartbeat message format compatible with pyzlc protocol
  - Heartbeat now sends 56+ bytes instead of full NodeInfo
  - Format: version (3×int32), node_id (36 bytes), info_id (int32), service_port (int32), group_name (variable)
- Change default multicast address from `239.255.10.10` to `224.0.0.1`
- Add `groupName` field with default value `zlc_default_group_name`
- Rename default service from `GetNodeInfo` to `get_node_info` (pyzlc compatible, no namespace prefix)

### Added
- `HeartbeatMessage` class in NetworkUtils.cs with Big-Endian serialization

## v2.0.0-beta.1 - 2025-09-24
- Unity version to 6000.0.65f1
- remove some packages
- update log streamer
- using msgpack
- using zerolancom framework

## v1.2.0
- Merge branch 'feat/node-info-update' (5d88985, 2025-09-24)
- update default sim material resolver (2bd4c86, 2025-09-24)
- Merge pull request #5 from intuitive-robots/feat/node-info-update (962888b, 2025-09-23)
- update version (5360e00, 2025-09-23)
- fix the bug of sceneloader service loaded delay (5e7c840, 2025-09-23)
- add material profile (b3508d3, 2025-09-19)
- upgrade node info for online node info update (59e7432, 2025-09-19)
- update package version (4720a09, 2025-09-18)

## v1.1.6 - 2025-09-18
- Initial commit (6c47e5e, 2025-04-14)
- first commit (a0e7210, 2025-04-14)
- update readme and create a new prefab (f9e59e0, 2025-04-15)
- update the package version (c3c13f0, 2025-04-15)
- fix the typo of asmdef (5e05888, 2025-04-15)
- add a origin indicator (727ac33, 2025-04-15)
- fix the URP color bug and add light (69ee5dc, 2025-04-16)
- add point cloud loader (d5775ef, 2025-04-25)
- XR server (c78265f, 2025-06-23)
- update code to support server-based IRIS XR application (967d12e, 2025-06-26)
- fix texture issue (fc82e4a, 2025-07-04)
- rigid body update (5b153fb, 2025-07-06)
- fix the issue of run in the background of URP (1fa0bf5, 2025-07-07)
- ready to merge to main branch (3c759d3, 2025-07-08)
- Merge pull request #1 from intuitive-robots/dev/multicast (aee0c2d, 2025-07-08)
- v1.0.5 fix the asmdef issue and remove some unused members (39ef44b, 2025-07-09)
- change the simdata setting and make it simpler (5365c25, 2025-07-10)
- update to v1.0.6 (e7e7229, 2025-07-10)
- update to multicast in all interfaces (3928d16, 2025-07-11)
- fix the bug of sceneloader doesn't unregister services (45ea4ba, 2025-07-11)
- initial service first then multicast (d50ee95, 2025-07-11)
- clean up NetMQ properly (cf97f7e, 2025-07-15)
- force asyncIO for Windows (9263110, 2025-07-15)
- format code (d7b16cc, 2025-07-15)
- accelerate the loading scene by compress the sim objects together (d13526d, 2025-07-15)
- update version (c058761, 2025-07-15)
- update service in a sub-thread to accelerate create assets (ce88fce, 2025-07-17)
- Merge pull request #2 from intuitive-robots/fix/socket_in_main_thread (5c259b8, 2025-07-17)
- change StartServiceTask to async make sub-thread running on Windows Editor (e336492, 2025-07-17)
- add publisher (63548f0, 2025-07-21)
- using new publisher and test the publisher (9efb79c, 2025-07-23)
- Merge branch 'test/windows-platform' into fix/logger (9291d3a, 2025-07-24)
- test the new publisher in windows and create a IRIS Node prefabs (b7e45a4, 2025-07-25)
- update to version 1.1.4 (d6c1ae4, 2025-07-25)
- remove unnecessary namespace (f3e31e6, 2025-07-25)
- add TransformUtils (8eeec66, 2025-07-28)
- update new TransformationUtils and change version to 1.1.5 (73ced2f, 2025-07-28)
- add GetSceneTransform (bb8c35c, 2025-07-28)
- IRIS anchor (ff222e3, 2025-07-31)
- create IRISAnchor and remove Tests folder (e5d37dc, 2025-08-01)
- Merge pull request #4 from intuitive-robots/feat/iris-anchor (e34db6a, 2025-08-01)
- Add GNU General Public License v3 (ded8b73, 2025-09-15)
- update icon and fix the bug of spawn/delect scene (5063af6, 2025-09-18)
