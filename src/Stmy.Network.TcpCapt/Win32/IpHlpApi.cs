using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Stmy.Network.TcpCapt.Win32
{
    static class IpHlpApi
    {
        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetBestInterface(uint dwDestAddr, out uint pdwBestIfIndex);

        public enum TcpTableClass : int
        {
            BasicListener,
            BasicConnections,
            BasicAll,
            OwnerPidListener,
            OwnerPidConnections,
            OwnerPidAll,
            OwnerModuleListener,
            OwnerModuleConnections,
            OwnerModuleAll
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcpRow
        {
            public System.Net.NetworkInformation.TcpState State;
            public uint LocalAddr;
            public int LocalPort;
            public uint RemoteAddr;
            public int RemotePort;
            public int ProcessId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcp6Row
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] LocalAddr;
            public uint LocalScopeId;
            public int LocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] RemoteAddr;
            public uint RemoteScopeId;
            public int RemotePort;
            public System.Net.NetworkInformation.TcpState State;
            public int ProcessId;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public extern static int GetExtendedTcpTable(
            IntPtr pTcpTable,
            out int pdwSize,
            bool bOrder,
            AddressFamily addressFamily,
            TcpTableClass tableClass,
            int reserved);
    }
}
