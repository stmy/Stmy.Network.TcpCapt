using System;

namespace Stmy.Network.TcpCapt
{
    public class ConnectionOpenedEventArgs : EventArgs
    {
        public Connection Connection { get; }

        internal ConnectionOpenedEventArgs(Connection connection)
        {
            this.Connection = connection;
        }
    }
}
