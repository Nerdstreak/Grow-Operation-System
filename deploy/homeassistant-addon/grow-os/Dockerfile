# The Home Assistant Supervisor builds this on the target device. It simply wraps
# the multi-arch Grow OS image already published to GHCR — Docker pulls the layer
# matching the device architecture (aarch64 / armv7 / amd64) automatically.
#
# The base image already sets ASPNETCORE_URLS=http://0.0.0.0:5076,
# GROWDIARY_DB_PATH=/data/grow-diary.db and the dotnet ENTRYPOINT. Add-ons get a
# persistent /data volume that Home Assistant includes in its backups, so the
# Grow OS SQLite database is backed up automatically with no extra configuration.
FROM ghcr.io/nerdstreak/grow-operation-system:latest
