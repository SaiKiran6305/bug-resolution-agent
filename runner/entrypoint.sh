#!/bin/sh
set -eu
cp -R /input/. /work/
cd /work
# No network and no provider credentials. This fixture uses only SDK libraries.
dotnet run --project "$1" --disable-build-servers
