# Docker availability

The private release pipeline builds server images for Linux amd64 and arm64 and
tests an amd64 Compose stack with MariaDB and Launcher Services. A public
container image and a standalone Compose deployment bundle are not distributed
with the 2.1.2 downloads.

Use the native **Core** archive for a server installation from this repository.
Follow the [runtime dependencies](BUILD_AND_RUNTIME_DEPENDENCIES.md),
[platform setup](README.md#install-a-release), and [hosting guide](HOSTED_SERVER.md).

The internal Docker build instructions require private project build files and
are not applicable to this documentation repository. No public registry URL or
image tag should be assumed from the application version. Container deployment
instructions can be added when a supported image and its configuration bundle
are distributed.
