using System;
using System.Net;
using System.Net.Sockets;
using PacketDotNet;

namespace Stmy.Network.TcpCapt.CapturingServices
{
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

        public event EventHandler<PacketCapturedEventArgs> Captured;

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

        public void Dispose()
        {
            socket.Close();
            socket.Dispose();
        }
    }
}
