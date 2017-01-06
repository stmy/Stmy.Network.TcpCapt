using System;

namespace Stmy.Network.TcpCapt
{
    public class PacketProcessedEventArgs : EventArgs
    {
        public TcpEndpoint Source { get; }
        public TcpEndpoint Destination { get; }
        public Direction Direction { get; }
        public Connection Connection { get; }
        public TcpStream Stream { get; }

        internal PacketProcessedEventArgs(
            TcpEndpoint source, 
            TcpEndpoint destination, 
            Direction direction,
            Connection connection)
        {
            this.Source = source;
            this.Destination = destination;
            this.Direction = direction;
            this.Connection = connection;
            this.Stream = direction == TcpCapt.Direction.Incoming 
                ? connection.Incoming
                : connection.Outgoing;
        }
    }
}
