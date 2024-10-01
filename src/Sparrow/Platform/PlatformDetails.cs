﻿using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Sparrow.Platform
{
    public static class PlatformDetails
    {
        public static readonly bool IsWindows8OrNewer;

        private static readonly bool IsWindows10OrNewer;

        public static readonly bool Is32Bits = IntPtr.Size == sizeof(int);

#if NET6_0_OR_GREATER
        [SupportedOSPlatformGuard("linux")]
        [SupportedOSPlatformGuard("osx")]
#endif
        public static readonly bool RunningOnPosix = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                                                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

#if NET6_0_OR_GREATER
        [SupportedOSPlatformGuard("osx")]
#endif
        public static readonly bool RunningOnMacOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

#if NET6_0_OR_GREATER
        [SupportedOSPlatformGuard("linux")]
#endif
        public static readonly bool RunningOnLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

#if NET6_0_OR_GREATER
        [SupportedOSPlatformGuard("windows")]
#endif
        public static readonly bool RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static readonly bool CanPrefetch;
        public static readonly bool CanDiscardMemory;
        internal static readonly bool CanUseHttp2;

        public static bool RunningOnDocker;

        static PlatformDetails()
        {
            RunningOnDocker = string.Equals(Environment.GetEnvironmentVariable("RAVEN_IN_DOCKER"), "true", StringComparison.OrdinalIgnoreCase);

            if (TryGetWindowsVersion(out var version))
            {
                IsWindows8OrNewer = version >= 6.19M;
                IsWindows10OrNewer = version >= 10M;
            }

            CanPrefetch = IsWindows8OrNewer || RunningOnPosix;
            CanDiscardMemory = IsWindows10OrNewer || RunningOnPosix;
            CanUseHttp2 = IsWindows10OrNewer || RunningOnPosix;
        }

        private static bool TryGetWindowsVersion(out decimal version)
        {
            version = -1M;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
                return false;

            try
            {
                const string winString = "Windows ";
                var os = RuntimeInformation.OSDescription;

                var idx = os.IndexOf(winString, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return false;

                var ver = os.Substring(idx + winString.Length);

                // remove second occurence of '.' (win 10 might be 10.123.456)
                var index = ver.IndexOf('.', ver.IndexOf('.') + 1);
                ver = string.Concat(ver.Substring(0, index), ver.Substring(index + 1));

                return decimal.TryParse(ver, NumberStyles.Any, CultureInfo.InvariantCulture, out version);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }
}
