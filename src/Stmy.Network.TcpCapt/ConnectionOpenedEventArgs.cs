using System;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents arguments for the <see cref="TcpCapturer.ConnectionOpened"/> event.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class ConnectionOpenedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the connection newly opened.
        /// </summary>
        public Connection Connection { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionOpenedEventArgs" /> class.
        /// </summary>
        /// <param name="connection">The connection newly opened.</param>
        internal ConnectionOpenedEventArgs(Connection connection)
        {
            this.Connection = connection;
        }
    }
}
