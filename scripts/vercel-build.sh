#!/usr/bin/env bash
set -euo pipefail

SDK_VERSION="10.0.400"
CACHE_ROOT="${HOME}/.cache/five-decisions"
export DOTNET_ROOT="${CACHE_ROOT}/dotnet/${SDK_VERSION}"
export DOTNET_CLI_HOME="${CACHE_ROOT}/home"
export PATH="${DOTNET_ROOT}:${PATH}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

source scripts/install-dotnet.sh
dotnet publish src/Vector.App -c Release
