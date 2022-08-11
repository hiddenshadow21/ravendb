#!/bin/bash -x

ASSETS_DIR=/assets
DEST_DIR=~/rpmbuild

export RAVENDB_VERSION_MINOR=$( grep -o -E '^[0-9]+.[0-9]+' <<< "$RAVENDB_VERSION" )

set -e

rpmdev-setuptree

MS_RPM_NAME="/cache/packages-microsoft-prod_${DISTRO_VERSION}.deb"
if ! (test -f "${MS_RPM_NAME}" \
    || wget -O "${MS_RPM_NAME}" "https://packages.microsoft.com/config/${DISTRO_NAME}/${DISTRO_VERSION}/packages-microsoft-prod.rpm"); then
     echo "Could not obtain packages-microsoft-prod.deb"
     exit 1
fi

yum -i $MS_RPM_NAME
yum update

DOWNLOAD_URL=https://daily-builds.s3.amazonaws.com/RavenDB-${RAVENDB_VERSION}-${RAVEN_PLATFORM}.tar.bz2 

export TARBALL="ravendb-${RAVENDB_VERSION}-${RAVEN_PLATFORM}.tar.bz2"
export CACHED_TARBALL="${TARBALL_CACHE_DIR}/${TARBALL}"

if ! (test -f "${CACHED_TARBALL}" || wget -O ${CACHED_TARBALL} -N --progress=dot:mega ${DOWNLOAD_URL}); then
    echo "Failed to download $DOWNLOAD_URL"
    exit 1
fi

DOTNET_FULL_VERSION=$(tar xf ${CACHED_TARBALL} RavenDB/runtime.txt -O | sed 's/\r$//' | sed -n "s/.NET Core Runtime: \([0-9.]\)/\1/p")
DOTNET_VERSION_MINOR=$(grep -o -E '^[0-9]+.[0-9]+' <<< $DOTNET_FULL_VERSION)
export DOTNET_DEPS_VERSION="$DOTNET_FULL_VERSION"
export DOTNET_RUNTIME_VERSION="$DOTNET_VERSION_MINOR"

# Show dependencies for amd64 since that's the only platform Microsoft ships package for,
# however the dependencies are the same at the moment.
DOTNET_RUNTIME_DEPS_PKG="dotnet-runtime-$DOTNET_RUNTIME_VERSION"
DOTNET_RUNTIME_DEPS=$(yum deplist $DOTNET_RUNTIME_DEPS_PKG 2>/dev/null | sed -n -e 's/\sdependency: //p')
if [ -z "$DOTNET_RUNTIME_DEPS" ]; then
    echo "Could not extract dependencies from $DOTNET_RUNTIME_DEPS_PKG package."
    exit 1
fi

export RPM_DEPS="${DOTNET_RUNTIME_DEPS}, libc6-dev (>= 2.27)"

echo ".NET Runtime: $DOTNET_FULL_VERSION"
echo "Package dependencies: $RPM_DEPS"

case $RAVEN_PLATFORM in

    linux-x64)
        export RPM_ARCHITECTURE="amd64"
        export RAVEN_SO_ARCH_SUFFIX="linux.x64"
        ;;

    linux-arm64)
        export RPM_ARCHITECTURE="arm64"
        export RAVEN_SO_ARCH_SUFFIX="arm.64"
        ;;

    raspberry-pi)
        export RPM_ARCHITECTURE="armhf"
        export RAVEN_SO_ARCH_SUFFIX="arm.32"
        ;;
    
    *)
        echo "Unsupported platform $RAVEN_PLATFORM for building a RPM package for."
        exit 1

esac

export VERSION=$RAVENDB_VERSION
export PACKAGE_REVISION="${PACKAGE_REVISION:-0}"
export PACKAGEVERSION="${VERSION}-${PACKAGE_REVISION}"
export PACKAGEFILENAME="ravendb_${PACKAGEVERSION}_${RPM_ARCHITECTURE}.rpm"

export RPMCHANGELOGDATE=$(date +"%a %b %d %Y")
export COPYRIGHT_YEAR=$(date +"%Y")

asset=/assets/ravendb/rpm/ravendb.spec
dest="$DEST_DIR/SPEC/$(basename $asset)"
mkdir -p $(dirname $dest)
envsubst < $asset > $dest

find /build -type d -exec chmod 755 {} +
find /build -type f -exec chmod 644 {} +

rpmbuild -ba $DEST_DIR/SPEC/ravendb.spec

cp -v $DEST_DIR/RPMS/**/*.rpm $OUTPUT_DIR

echo "Package contents:"
rpm -qlp "${OUTPUT_DIR}/${PACKAGEFILENAME}" | tee $OUTPUT_DIR/rpm_contents.txt

echo "Package info:"
rpm -qip "${OUTPUT_DIR}/${PACKAGEFILENAME}" | tee $OUTPUT_DIR/rpm_info.txt
