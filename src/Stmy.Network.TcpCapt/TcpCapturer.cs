using System;
using System.Collections.Generic;
using System.Linq;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Provides functionality of TCP data stream capturing for specific process.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class TcpCapturer : IDisposable
    {
        bool firstTime;
        readonly int targetProcessId;
        readonly ICapturingService service;
        List<Connection> connections;
        List<SocketOwnerInfo> socketInfoes;

        /// <summary>
        /// Occurs when new connection has opened.
        /// </summary>
        public event EventHandler<ConnectionOpenedEventArgs> ConnectionOpened;

        /// <summary>
        /// Occurs when packet has processed.
        /// </summary>
        public event EventHandler<PacketProcessedEventArgs> PacketProcessed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpCapturer"/> class.
        /// </summary>
        /// <param name="targetProcessId">The target process to capture.</param>
        /// <param name="service">The capturing service.</param>
        /// <exception cref="System.ArgumentNullException">Throws when <paramref name="service"/> is <c>null</c>.</exception>
        public TcpCapturer(int targetProcessId, ICapturingService service)
        {
            if (service == null) { throw new ArgumentNullException(nameof(service)); }

            this.targetProcessId = targetProcessId;
            this.service = service;
            this.service.Captured += OnCaptured;
            this.connections = new List<Connection>();
            this.socketInfoes = new List<SocketOwnerInfo>();
        }

        /// <summary>
        /// Starts the capturing.
        /// </summary>
        public void Start()
        {
            firstTime = true;
            service.StartCapture();
        }

        /// <summary>
        /// Stops the capturing.
        /// </summary>
        public void Stop()
        {
            service.StopCapture();
        }

        /// <summary>
        /// Stops the capturing and disposes resources.
        /// </summary>
        public void Dispose()
        {
            service.Captured -= OnCaptured;
        }

        void OnCaptured(object sender, PacketCapturedEventArgs e)
        {
            var packet = e.Packet;
            if (packet == null) { return; }

            var ipPacket = (IpPacket)packet.Extract(typeof(IpPacket));
            var tcpPacket = (TcpPacket)packet.Extract(typeof(TcpPacket));
            if (ipPacket == null || tcpPacket == null) { return; }

            var source = new TcpEndpoint(ipPacket.SourceAddress, tcpPacket.SourcePort);
            var dest = new TcpEndpoint(ipPacket.DestinationAddress, tcpPacket.DestinationPort);

            Direction direction;
            if (!TryGetDirection(tcpPacket, source, dest, out direction)) { return; }

            var connection = connections.Where(c => c.IsAcceptable(source, dest)).FirstOrDefault();
            if (connection == null) // New connection!
            {
                if (direction == Direction.Outgoing)
                {
                    connection = new Connection(source, dest);
                }
                else
                {
                    connection = new Connection(dest, source);
                }
                connections.Add(connection);
                // TODO: Add functionality to remove connections which is closed and no longer used

                ConnectionOpened?.Invoke(this, new ConnectionOpenedEventArgs(connection));
            }

            connection.Process(packet);

            PacketProcessed?.Invoke(this, new PacketProcessedEventArgs(source, dest, direction, connection));
        }

        bool TryGetDirection(TcpPacket packet, TcpEndpoint source, TcpEndpoint destination, out Direction direction)
        {
            lock (socketInfoes)
            {
                // Update socket information on first-time execution or
                // TCP connection establishment (SYN)
                if (firstTime || (packet.Syn && !packet.Ack))
                {
                    UpdateSocketList();
                    firstTime = false;
                }

                direction = socketInfoes
                    .Select(s => s.GetDirection(source, destination))
                    .Where(d => d != Direction.Invalid)
                    .FirstOrDefault();

                return direction != Direction.Invalid;
            }
        }

        void UpdateSocketList()
        {
            socketInfoes.Clear();
            socketInfoes.AddRange(Util.GetIPv4SocketOwners().Where(x => x.ProcessId == targetProcessId));
            socketInfoes.AddRange(Util.GetIPv6SocketOwners().Where(x => x.ProcessId == targetProcessId));
        }
    }
}
