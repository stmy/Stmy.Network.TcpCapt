using System;
using System.Net;

namespace Stmy.Network.TcpCapt
{
    public class TcpEndpoint
    {
        public IPAddress Address { get; }
        public int Port { get; }

        public TcpEndpoint(IPAddress address, int port)
        {
            if (address == null) { throw new ArgumentNullException(nameof(address)); }
            if (port < 0) { throw new ArgumentOutOfRangeException(nameof(port)); }
            if (port > 65535) { throw new ArgumentOutOfRangeException(nameof(port)); }

            Address = address;
            Port = port;
        }

        public bool Equals(TcpEndpoint value)
        {
            if (value == null) { return false; }
            return Port == value.Port && Address.Equals(value.Address);
        }

        public override int GetHashCode()
        {
            return unchecked(Address.GetHashCode() * Port.GetHashCode());
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TcpEndpoint);
        }

        public static bool operator ==(TcpEndpoint lhs, TcpEndpoint rhs)
        {
            if (ReferenceEquals(lhs, null)) return ReferenceEquals(rhs, null);
            else return lhs.Equals(rhs);
        }

        public static bool operator !=(TcpEndpoint lhs, TcpEndpoint rhs)
        {
            return !(lhs == rhs);
        }
    }
}
