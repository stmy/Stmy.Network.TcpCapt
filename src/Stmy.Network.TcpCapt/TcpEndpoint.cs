using System;
using System.Net;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents TCP endpoint.
    /// </summary>
    public class TcpEndpoint
    {
        /// <summary>
        /// Gets the IP address.
        /// </summary>
        public IPAddress Address { get; }

        /// <summary>
        /// Gets the port number.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpEndpoint"/> class.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="port">The port number.</param>
        /// <exception cref="System.ArgumentNullException">Throws when <paramref name="address"/> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     Throws when <paramref name="port"/> is not in range 0 to 65535.
        /// </exception>
        public TcpEndpoint(IPAddress address, int port)
        {
            if (address == null) { throw new ArgumentNullException(nameof(address)); }
            if (port < 0) { throw new ArgumentOutOfRangeException(nameof(port)); }
            if (port > 65535) { throw new ArgumentOutOfRangeException(nameof(port)); }

            Address = address;
            Port = port;
        }

        /// <summary>
        /// Equalses the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if <paramref name="value"/> represents same TCP endpoint; otherwise, <c>false</c>.</returns>
        public bool Equals(TcpEndpoint value)
        {
            if (value == null) { return false; }
            return Port == value.Port && Address.Equals(value.Address);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return unchecked(Address.GetHashCode() * Port.GetHashCode());
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as TcpEndpoint);
        }

        /// <summary>
        /// Determines whether the specified two <see cref="TcpEndpoint" /> is equal or not.
        /// </summary>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <returns>
        ///     <c>true</c> if two <see cref="TcpEndpoint" /> represents same TCP endpoint; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator ==(TcpEndpoint lhs, TcpEndpoint rhs)
        {
            if (ReferenceEquals(lhs, null)) return ReferenceEquals(rhs, null);
            else return lhs.Equals(rhs);
        }

        /// <summary>
        /// Determines whether the specified two <see cref="TcpEndpoint" /> isn't equal or not.
        /// </summary>
        /// <param name="lhs">The LHS.</param>
        /// <param name="rhs">The RHS.</param>
        /// <returns>
        ///     <c>true</c> if two <see cref="TcpEndpoint" /> represents different TCP endpoint; otherwise, <c>false</c>.
        /// </returns>
        public static bool operator !=(TcpEndpoint lhs, TcpEndpoint rhs)
        {
            return !(lhs == rhs);
        }
    }
}
