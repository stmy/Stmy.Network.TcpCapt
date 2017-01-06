using System;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    public class Connection : IDisposable
    {
        public TcpEndpoint Source { get; }
        public TcpEndpoint Destination { get; }

        public TcpStream Incoming { get; }
        public TcpStream Outgoing { get; }

        public bool Disposed { get; private set; }
        
        public Connection(TcpEndpoint source, TcpEndpoint destination)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (source == null) { throw new ArgumentNullException(nameof(destination)); }

            Source = source;
            Destination = destination;
            Incoming = new TcpStream();
            Outgoing = new TcpStream();
            Disposed = false;
        }

        internal void Process(Packet packet)
        {
            if (packet == null) { throw new ArgumentNullException(nameof(packet)); }
            if (Disposed) { throw new ObjectDisposedException(GetType().FullName); }

            var tcpPacket = (TcpPacket)packet.Extract(typeof(TcpPacket));
            if (tcpPacket == null)
            {
                return;
            }

            var direction = GetPacketDirection(packet);
            if (direction == Direction.Outgoing)
            {
                // Outgoing packet has not valid checksum due to the value is set by the NIC
                // and is not necessary to check anyways
                Outgoing.Process(tcpPacket, useChecksum: false);
            }
            else if (direction == Direction.Incoming)
            {
                Incoming.Process(tcpPacket);
            }
        }

        Direction GetPacketDirection(Packet packet)
        {
            var ipPacket = (IpPacket)packet.Extract(typeof(IpPacket));
            var tcpPacket = (TcpPacket)packet.Extract(typeof(TcpPacket));
            if (ipPacket == null || tcpPacket == null)
            {
                return Direction.Invalid;
            }

            var source = new TcpEndpoint(ipPacket.SourceAddress, tcpPacket.SourcePort);
            var dest = new TcpEndpoint(ipPacket.DestinationAddress, tcpPacket.DestinationPort);

            return GetPacketDirection(source, dest);
        }

        Direction GetPacketDirection(TcpEndpoint source, TcpEndpoint dest)
        {
            if (source == Source && dest == Destination)
            {
                return Direction.Outgoing;
            }
            else if (source == Destination && dest == Source)
            {
                return Direction.Incoming;
            }

            return Direction.Invalid;
        }

        public bool IsAcceptable(TcpEndpoint source, TcpEndpoint dest)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (source == null) { throw new ArgumentNullException(nameof(dest)); }

            var direction = GetPacketDirection(source, dest);
            return direction == Direction.Incoming || direction == Direction.Outgoing;
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            Disposed = true;
        }
    }
}