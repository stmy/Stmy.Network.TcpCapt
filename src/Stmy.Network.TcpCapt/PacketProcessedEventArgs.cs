using System;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents arguments for the <see cref="TcpCapturer.PacketProcessed"/> event.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class PacketProcessedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the source TCP endpoint of the processed packet.
        /// </summary>
        public TcpEndpoint Source { get; }

        /// <summary>
        /// Gets the destination TCP endpoint of the processed packet.
        /// </summary>
        public TcpEndpoint Destination { get; }

        /// <summary>
        /// Gets the direction of the processed packet.
        /// </summary>
        public Direction Direction { get; }

        /// <summary>
        /// Gets the connection for the packet.
        /// </summary>
        public Connection Connection { get; }

        /// <summary>
        /// Gets the TCP data stream for the packet.
        /// </summary>
        public TcpStream Stream { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketProcessedEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source TCP endpoint of processed packet.</param>
        /// <param name="destination">The destination TCP endpoint of processed packet.</param>
        /// <param name="direction">The direction of processed packet.</param>
        /// <param name="connection">The connection for processed packet.</param>
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
