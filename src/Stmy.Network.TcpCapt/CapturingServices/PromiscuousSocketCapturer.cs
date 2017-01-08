using System;
using System.Net;
using System.Net.Sockets;
using PacketDotNet;

namespace Stmy.Network.TcpCapt.CapturingServices
{
    /// <summary>
    /// Provides packet capturing function for single network interface.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    class PromiscuousSocketCapturer : IDisposable
    {
        static readonly byte[] FakeEthernetHeaderV4 =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Source mac address
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Destination mac address
            0x08, 0x00                          // Type = IP (0x0800)
        };

        static readonly byte[] FakeEthernetHeaderV6 =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Source mac address
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Destination mac address
            0x08, 0xDD                          // Type = IPv6 (0x08DD)
        };

        readonly Socket socket;
        readonly byte[] buffer;
        readonly byte[] fakeEthernetHeader;

        /// <summary>
        /// Occurs when packet is captured.
        /// </summary>
        public event EventHandler<PacketCapturedEventArgs> Captured;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromiscuousSocketCapturer"/> class.
        /// </summary>
        /// <param name="address">The address of network interface to capture.</param>
        /// <exception cref="System.ArgumentNullException">Throws when <paramref name="address"/> is <c>null</c></exception>
        /// <exception cref="System.ArgumentException">Throws when <see cref="AddressFamily"/> of <paramref name="address"/> is not supported.</exception>
        public PromiscuousSocketCapturer(IPAddress address)
        {
            if (address == null) { throw new ArgumentNullException(nameof(address)); }
            if (!(address.AddressFamily == AddressFamily.InterNetwork &&
                  address.AddressFamily == AddressFamily.InterNetworkV6))
            {
                throw new ArgumentException($"Only IPv4/IPv6 is supported", nameof(address));
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                fakeEthernetHeader = FakeEthernetHeaderV4;
                buffer = new byte[65535];
            }
            else
            {
                fakeEthernetHeader = FakeEthernetHeaderV6;
                buffer = new byte[65535 + 40];
            }

            socket = new Socket(address.AddressFamily, SocketType.Raw, ProtocolType.IP);
            socket.Bind(new IPEndPoint(address, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            socket.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 0 });
            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
        }

        void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                var bytesReceived = socket.EndReceive(ar);
                if (bytesReceived > 0)
                {
                    // Prepend fake ethernet header
                    byte[] packetData = new byte[fakeEthernetHeader.Length + bytesReceived];
                    Array.Copy(fakeEthernetHeader, packetData, fakeEthernetHeader.Length);
                    Array.Copy(buffer, 0, packetData, fakeEthernetHeader.Length, bytesReceived);
                    
                    Captured?.Invoke(this, new PacketCapturedEventArgs(Packet.ParsePacket(LinkLayers.Ethernet, packetData)));
                }
            }
            catch { }
            finally
            {
                try
                {
                    if (socket != null)
                    {
                        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            socket.Close();
            socket.Dispose();
        }
    }
}
