using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using static Stmy.Network.TcpCapt.Win32.IpHlpApi;

namespace Stmy.Network.TcpCapt
{
    class Util
    {
        public static IPAddress[] GetLocalAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(ni => ni.GetIPProperties())
                .SelectMany(niProp => niProp.UnicastAddresses.Select(uipInfo => uipInfo.Address))
                .Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
        }

        public static IEnumerable<SocketOwnerInfo> GetIPv4SocketOwners()
        {
            const int NoError = 0;

            // Get required buffer size
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, out bufferSize, true, AddressFamily.InterNetwork, TcpTableClass.OwnerPidAll, 0);

            // Get all IPv4 TCP sockets
            var tcpTable = Marshal.AllocHGlobal(bufferSize);
            if (GetExtendedTcpTable(tcpTable, out bufferSize, true, AddressFamily.InterNetwork, TcpTableClass.OwnerPidAll, 0) != NoError)
            {
                Marshal.FreeHGlobal(tcpTable);
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // typedef struct {
            //   DWORD dwNumEntries;
            //   MIB_TCPROW_OWNER_PID table[ANY_SIZE];
            // } MIB_TCPTABLE_OWNER_PID, * PMIB_TCPTABLE_OWNER_PID;

            try
            {
                var numEntries = Marshal.ReadInt32(tcpTable);
                var rowSize = Marshal.SizeOf<MibTcpRow>();
                for (var i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRow>(new IntPtr(tcpTable.ToInt64() + 4 + rowSize * i));
                    yield return new SocketOwnerInfo(row);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTable);
            }
        }

        public static IEnumerable<SocketOwnerInfo> GetIPv6SocketOwners()
        {
            const int NoError = 0;

            // Get required buffer size
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, out bufferSize, true, AddressFamily.InterNetworkV6, TcpTableClass.OwnerPidAll, 0);

            // Get all IPv6 TCP sockets
            var tcpTable = Marshal.AllocHGlobal(bufferSize);
            if (GetExtendedTcpTable(tcpTable, out bufferSize, true, AddressFamily.InterNetworkV6, TcpTableClass.OwnerPidAll, 0) != NoError)
            {
                Marshal.FreeHGlobal(tcpTable);
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // typedef struct _MIB_TCP6TABLE_OWNER_PID
            // {
            //     DWORD dwNumEntries;
            //     MIB_TCP6ROW_OWNER_PID table[ANY_SIZE];
            // }
            // MIB_TCP6TABLE_OWNER_PID, *PMIB_TCP6TABLE_OWNER_PID;

            try
            {
                var numEntries = Marshal.ReadInt32(tcpTable);
                var rowSize = Marshal.SizeOf<MibTcp6Row>();
                for (var i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcp6Row>(new IntPtr(tcpTable.ToInt64() + 4 + rowSize * i));
                    yield return new SocketOwnerInfo(row);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTable);
            }
        }
    }
}
