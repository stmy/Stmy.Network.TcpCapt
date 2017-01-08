using System;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents TCP connection.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class Connection : IDisposable
    {
        /// <summary>
        /// Gets the source TCP endpoint.
        /// </summary>
        public TcpEndpoint Source { get; }

        /// <summary>
        /// Gets the destination TCP endpoint.
        /// </summary>
        public TcpEndpoint Destination { get; }

        /// <summary>
        /// Gets the incoming TCP data stream.
        /// </summary>
        public TcpStream Incoming { get; }

        /// <summary>
        /// Gets the outgoing TCP data stream.
        /// </summary>
        public TcpStream Outgoing { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Connection"/> is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if disposed; otherwise, <c>false</c>.
        /// </value>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="source">The source TCP endpoint.</param>
        /// <param name="destination">The destination TCP endpoint.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     Throws when <paramref name="source"/> and/or <paramref name="destination"/> is <c>null</c>.
        /// </exception>
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

        /// <summary>
        /// Processes the specified TCP packet.
        /// Do nothing when the packet is not TCP packet.
        /// </summary>
        /// <param name="packet">The packet to process.</param>
        /// <exception cref="System.ArgumentNullException">Throws when <paramref name="packet"/> is <c>null</c>.</exception>
        /// <exception cref="System.ObjectDisposedException">Throws when the connection is already disposed.</exception>
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

        /// <summary>
        /// Gets the TCP packet direction.
        /// </summary>
        /// <param name="packet">The packet to determine direction.</param>
        /// <returns>Direction of TCP packet. Returns <see cref="Direction.Invalid"/> when specified packet is not for this connection or the packet is invalid.</returns>
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

        /// <summary>
        /// Gets the direction from TCP endpoints.
        /// </summary>
        /// <param name="source">The source TCP endpoint.</param>
        /// <param name="dest">The destination TCP endpoint.</param>
        /// <returns>Direction of TCP endpoints. Returns <see cref="Direction.Invalid"/> when specified packet is not for this connection.</returns>
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

        /// <summary>
        /// Determines whether the specified TCP endpoints is acceptable.
        /// </summary>
        /// <param name="source">The source TCP endpoint.</param>
        /// <param name="dest">The destination TCP endpoint.</param>
        /// <returns>
        ///   <c>true</c> if the specified source is acceptable; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        ///     Throws when <paramref name="source"/> and/or <paramref name="dest"/> is <c>null</c>.
        /// </exception>
        public bool IsAcceptable(TcpEndpoint source, TcpEndpoint dest)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (source == null) { throw new ArgumentNullException(nameof(dest)); }

            var direction = GetPacketDirection(source, dest);
            return direction == Direction.Incoming || direction == Direction.Outgoing;
        }

        /// <summary>
        /// Disposes incoming and outgoing stream.
        /// </summary>
        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            Disposed = true;
        }
    }
}