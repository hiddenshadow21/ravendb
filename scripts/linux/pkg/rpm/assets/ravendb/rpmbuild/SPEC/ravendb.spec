Name:           ravendb
Version:        5.4.1
Release:        1%{?dist}
Summary:        RavenDB NoSQL Database
Group:          Applications/Databases
License:        MIT
URL:            https://github.com/ravendb/ravendb
Source0:        RavenDB-%{version}-linux-x64.tar.bz2

Requires: $RPM_DEPS 

AutoReqProv: no

%description
A NoSQL Database that's fully transactional. 
Allows 1 million reads and 150000 writes per second.

%prep
%setup -q -n RavenDB
rm -v *.sh

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/usr/lib/%{name}
cp -r ./* %{buildroot}/usr/lib/%{name}

%post -e

if [[ $1 -eq 1 ]]; then
    adduser --system --home /var/empty --no-create-home --user-group ravendb

    ldconfig
fi

RVN_RUNTIME_DIRS=(
    /etc/ravendb
    /etc/ravendb/security
    /var/lib/ravendb
    /var/lib/ravendb/{data,nuget}
    /var/log/ravendb/{logs,audit}
)

for runtimeDir in "${RVN_RUNTIME_DIRS[@]}"
do

mkdir -p $runtimeDir
chown root:ravendb $runtimeDir
chmod 770 $runtimeDir

done

chown root:ravendb %{_libdir}/ravendb/server/{Raven.Server,rvn}

if command -v setcap &> /dev/null; then
    setcap CAP_NET_BIND_SERVICE=+eip %{_libdir}/ravendb/server/Raven.Server
fi

CREATEDUMP_PATH="%{_libdir}/ravendb/server/createdump"
RAVEN_DEBUG_PATH="%{_libdir}/ravendb/server/Raven.Debug"
debugBinList=("$CREATEDUMP_PATH" "$RAVEN_DEBUG_PATH")
for binFilePath in "${debugBinList[@]}"
do
    binFilename="$(basename $binFilePath)"

    echo "Adjust $binFilename binary permissions..."
    if [[ ! -f "$binFilePath" ]]; then
        echo "$binFilename binary not found in under $binFilePath. Exiting..."
        exit 2
    fi

    if command -v setcap &> /dev/null; then
        setcap cap_sys_ptrace=eip "$binFilePath"
    fi

    chown root:ravendb "$binFilePath"
    chmod +s "$binFilePath"
done

if [ ! -f /etc/ravendb/settings.json ]; then
    cp %{_libdir}/ravendb/server/settings.default.json /etc/ravendb/settings.json
fi

if [ ! -f /etc/ravendb/security/master.key ]; then
    touch /etc/ravendb/security/master.key
fi

chmod 660 /etc/ravendb/security/master.key /etc/ravendb/settings.json
chown -R root:ravendb /etc/ravendb

#DEBHELPER#

ln -s %{_libdir}/ravendb/server/rvn /usr/bin/rvn
ln -s %{_libdir}/ravendb/server/libmscordaccore.so %{_libdir}/ravendb/server/libmscordaccore/libmscordaccore.so

if grep -e '"Setup.Mode": "Initial"' /etc/ravendb/settings.json >&/dev/null; then
    rvnPort="53700"
    fwdPort="8080"
    sshTunnelLine="ssh -N -L localhost:${fwdPort}:localhost:${rvnPort}"
    rvnServerAddr=$(grep -E '"ServerUrl"' /etc/ravendb/settings.json | grep -o -E 'http:[^"]+')
    user="${SUDO_USER:-username}"
    publicAddr="target-machine.com"
    if command -v dig >&/dev/null; then
        foundPublicAddr=$(dig +short myip.opendns.com @resolver1.opendns.com)
    elif command -v curl >&/dev/null; then
        foundPublicAddr=$(curl -s https://api.ipify.org)
    fi

    if [ $? -eq 0 ] && [ ! -z "$foundPublicAddr" ]; then
        publicAddr="$foundPublicAddr"
    fi

    echo "### RavenDB Setup ###"
    echo "#"
    echo "#  Please navigate to $rvnServerAddr in your web browser to complete setting up RavenDB."
    echo "#  If you set up the server through SSH, you can tunnel RavenDB setup port and proceed with the setup on your local."
    echo "#"
    echo "#  For public address:    $sshTunnelLine ${user}@${publicAddr}"
    echo "#"
    echo "#  For internal address:  $sshTunnelLine ${user}@$(hostname)"
    echo "#"
    echo "###"
fi

%postun
unlink /usr/bin/rvn
unlink ${_libdir}/ravendb/server/libmscordaccore/libmscordaccore.so

%clean
rm -rf %{buildroot}

%files
usr/lib/%{name}/Server

%changelog
* $DEBCHANGELOGDATE $DEBFULLNAME <$DEBEMAIL>
- [Full changelog available]
  https://ravendb.net/docs/article-page/$RAVENDB_VERSION_MINOR/csharp/start/whats-new