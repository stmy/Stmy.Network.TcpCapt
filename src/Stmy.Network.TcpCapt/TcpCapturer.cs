using System;
using System.Collections.Generic;
using System.Linq;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    public class TcpCapturer : IDisposable
    {
        bool firstTime;
        readonly int targetProcessId;
        readonly ICapturingService service;
        List<Connection> connections;
        List<SocketOwnerInfo> socketInfoes;

        public TcpCapturer(int targetProcessId, ICapturingService service)
        {
            if (service == null) { throw new ArgumentNullException(nameof(service)); }

            this.targetProcessId = targetProcessId;
            this.service = service;
            this.service.Captured += OnCaptured;
            this.connections = new List<Connection>();
            this.socketInfoes = new List<SocketOwnerInfo>();
        }

        public void Start()
        {
            firstTime = true;
            service.StartCapture();
        }

        public void Stop()
        {
            service.StopCapture();
        }

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

                //ConnectionOpened?.Invoke(this, new TcpConnectionEventArgs(connection));
            }

            connection.Process(packet);
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
