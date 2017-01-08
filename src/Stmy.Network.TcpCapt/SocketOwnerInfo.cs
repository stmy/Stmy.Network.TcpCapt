using System.Net;
using System.Net.NetworkInformation;
using static Stmy.Network.TcpCapt.Win32.IpHlpApi;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents TCP socket owner process information.
    /// </summary>
    class SocketOwnerInfo
    {
        public TcpEndpoint Local { get; }
        public TcpEndpoint Remote { get; }
        public int ProcessId { get; }
        public TcpState State { get; }

        public SocketOwnerInfo(MibTcpRow rawInfo)
        {
            var localAddr = new IPAddress(rawInfo.LocalAddr);
            var localPort = (rawInfo.LocalPort & 0xFF) << 8 | (rawInfo.LocalPort & 0xFF00) >> 8;
            var remoteAddr = new IPAddress(rawInfo.RemoteAddr);
            var remotePort = (rawInfo.RemotePort & 0xFF) << 8 | (rawInfo.RemotePort & 0xFF00) >> 8;
            Local = new TcpEndpoint(localAddr, localPort);
            Remote = new TcpEndpoint(remoteAddr, remotePort);
            ProcessId = rawInfo.ProcessId;
            State = rawInfo.State;
        }

        public SocketOwnerInfo(MibTcp6Row rawInfo)
        {
            var localAddr = new IPAddress(rawInfo.LocalAddr);
            var localPort = (rawInfo.LocalPort & 0xFF) << 8 | (rawInfo.LocalPort & 0xFF00) >> 8;
            var remoteAddr = new IPAddress(rawInfo.RemoteAddr);
            var remotePort = (rawInfo.RemotePort & 0xFF) << 8 | (rawInfo.RemotePort & 0xFF00) >> 8;
            Local = new TcpEndpoint(localAddr, localPort);
            Remote = new TcpEndpoint(remoteAddr, remotePort);
            ProcessId = rawInfo.ProcessId;
            State = rawInfo.State;
        }

        public Direction GetDirection(TcpEndpoint source, TcpEndpoint destination)
        {
            if (Local == source && Remote == destination)
            {
                return Direction.Outgoing;
            }
            else if (Local == destination && Remote == source)
            {
                return Direction.Incoming;
            }
            else
            {
                return Direction.Invalid;
            }
        }
    }
}
