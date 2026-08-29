#!/usr/bin/env bash
set -euo pipefail

SDK_VERSION="10.0.400"
SDK_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.400/dotnet-sdk-10.0.400-linux-x64.tar.gz"
# SDK_URL and SDK_SHA512 come from the official .NET 10 release metadata.
# https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json
SDK_SHA512="1033977dd837150e0814cf0c5d5b17ceb63925fda7ba2158b47258a4bd7c048cf82eac3bc1166f3146f53124a3f5fba09db1de1260d2ce96399860303b404b48"

CACHE_ROOT="${HOME}/.cache/five-decisions"
DOTNET_ROOT="${CACHE_ROOT}/dotnet/${SDK_VERSION}"
DOTNET_CLI_HOME="${CACHE_ROOT}/home"
ARCHIVE_PATH="${CACHE_ROOT}/downloads/dotnet-sdk-${SDK_VERSION}-linux-x64.tar.gz"
export DOTNET_ROOT DOTNET_CLI_HOME
export PATH="${DOTNET_ROOT}:${PATH}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

mkdir -p "${CACHE_ROOT}/downloads" "${DOTNET_CLI_HOME}"

if [[ ! -x "${DOTNET_ROOT}/dotnet" ]]; then
  if [[ ! -f "${ARCHIVE_PATH}" ]]; then
    curl --fail --location --retry 3 --silent --show-error \
      --output "${ARCHIVE_PATH}.part" "${SDK_URL}"
    mv "${ARCHIVE_PATH}.part" "${ARCHIVE_PATH}"
  fi

  printf '%s  %s\n' "${SDK_SHA512}" "${ARCHIVE_PATH}" | sha512sum --check --status

  staging="${DOTNET_ROOT}.staging.$$"
  rm -rf "${staging}"
  mkdir -p "${staging}"
  tar -xzf "${ARCHIVE_PATH}" -C "${staging}"
  test -x "${staging}/dotnet"
  rm -rf "${DOTNET_ROOT}"
  mv "${staging}" "${DOTNET_ROOT}"
fi

test "$("${DOTNET_ROOT}/dotnet" --version)" = "${SDK_VERSION}"
printf '%s\n' "${DOTNET_ROOT}"
