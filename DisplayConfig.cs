using System.Runtime.InteropServices;

namespace HdrTray;

record DisplayInfo(string Name, string DevicePath, bool HdrSupported, bool HdrEnabled, LUID AdapterId = default, uint TargetId = 0);

[StructLayout(LayoutKind.Sequential)]
readonly record struct LUID(uint LowPart, int HighPart);

// Reads and sets per-monitor HDR through the Display Configuration API (QueryDisplayConfig / DisplayConfigGetDeviceInfo).
// Windows 11 24H2 added separate HDR info/state requests; older builds only have the "advanced color" ones.
static class DisplayConfig
{
    public static List<DisplayInfo> GetDisplays()
    {
        var result = new List<DisplayInfo>();
        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != 0)
            return result;
        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            return result;

        for (int i = 0; i < pathCount; i++)
        {
            var target = paths[i].targetInfo;
            var (name, devicePath) = GetTargetName(target.adapterId, target.id, target.outputTechnology, i + 1);
            if (result.Any(d => d.DevicePath == devicePath)) continue;
            var (supported, enabled) = GetHdrState(target.adapterId, target.id);
            result.Add(new DisplayInfo(name, devicePath, supported, enabled, target.adapterId, target.id));
        }
        return result;
    }

    public static bool SetHdr(DisplayInfo display, bool enable)
    {
        if (TryGetColorInfo2(display.AdapterId, display.TargetId, out _))
        {
            var state = new DISPLAYCONFIG_SET_HDR_STATE
            {
                header = Header<DISPLAYCONFIG_SET_HDR_STATE>(DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE, display.AdapterId, display.TargetId),
                value = enable ? 1u : 0u,
            };
            return DisplayConfigSetDeviceInfo(ref state) == 0;
        }

        var legacy = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = Header<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE, display.AdapterId, display.TargetId),
            value = enable ? 1u : 0u,
        };
        return DisplayConfigSetDeviceInfo(ref legacy) == 0;
    }

    static (bool supported, bool enabled) GetHdrState(LUID adapter, uint id)
    {
        if (TryGetColorInfo2(adapter, id, out var info2))
        {
            // The user setting rather than activeColorMode, so HDR still reads as on while a policy
            // (e.g. battery saver) suspends it
            bool supported = (info2.value & (1u << 4)) != 0;   // highDynamicRangeSupported
            bool userEnabled = (info2.value & (1u << 5)) != 0; // highDynamicRangeUserEnabled
            return (supported, supported && userEnabled);
        }

        var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = Header<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO, adapter, id),
        };
        if (DisplayConfigGetDeviceInfo(ref info) != 0) return (false, false);
        // Wide color enforced = an SDR display using auto color management, which isn't HDR
        bool advancedSupported = (info.value & 1u) != 0;
        bool advancedEnabled = (info.value & 2u) != 0;
        bool wideColorEnforced = (info.value & 4u) != 0;
        return (advancedSupported && !wideColorEnforced, advancedEnabled && !wideColorEnforced);
    }

    // Succeeds only on Windows 11 24H2+, which is also when SET_HDR_STATE must be used
    static bool TryGetColorInfo2(LUID adapter, uint id, out DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 info)
    {
        info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            header = Header<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2, adapter, id),
        };
        return DisplayConfigGetDeviceInfo(ref info) == 0;
    }

    static (string name, string devicePath) GetTargetName(LUID adapter, uint id, uint outputTechnology, int index)
    {
        var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header = Header<DISPLAYCONFIG_TARGET_DEVICE_NAME>(DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, adapter, id),
        };
        string name = "", devicePath = "";
        if (DisplayConfigGetDeviceInfo(ref target) == 0)
        {
            name = target.monitorFriendlyDeviceName ?? "";
            devicePath = target.monitorDevicePath ?? "";
        }
        if (name.Length == 0)
            name = outputTechnology == DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL ? "Built-in display" : $"Display {index}";
        // The device path is stable across reboots and is what settings store; fall back to the adapter/target pair
        if (devicePath.Length == 0)
            devicePath = $"{adapter.HighPart:X8}{adapter.LowPart:X8}:{id}";
        return (name, devicePath);
    }

    static DISPLAYCONFIG_DEVICE_INFO_HEADER Header<T>(int type, LUID adapter, uint id) where T : struct =>
        new() { type = type, size = (uint)Marshal.SizeOf<T>(), adapterId = adapter, id = id };

    const uint QDC_ONLY_ACTIVE_PATHS = 0x2;
    const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
    const int DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;
    const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15;
    const int DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE = 16;
    const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000;

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    // Only the buffer size matters; the union contents are never read
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
        public uint activeColorMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DISPLAYCONFIG_SET_HDR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
    }

    [DllImport("user32.dll")]
    static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

    [DllImport("user32.dll")]
    static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_HDR_STATE setPacket);
}
