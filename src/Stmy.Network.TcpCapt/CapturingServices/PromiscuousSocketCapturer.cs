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

        public Socket SocketV4 { get; }
        public Socket SocketV6 { get; }
        byte[] bufferV4;
        byte[] bufferV6;

        public event EventHandler<PacketCapturedEventArgs> Captured;

        public PromiscuousSocketCapturer(IPAddress address)
        {

            bufferV4 = new byte[65535];
            SocketV4 = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            SocketV4.Bind(new IPEndPoint(address, 0));
            SocketV4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            SocketV4.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 0 });
            SocketV4.BeginReceive(bufferV4, 0, bufferV4.Length, SocketFlags.None, new AsyncCallback(ReceiveCallbackV4), null);

            bufferV6 = new byte[65535 + 40]; // not support jumbogram
            SocketV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IP);
            SocketV6.Bind(new IPEndPoint(address, 0));
            SocketV6.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            SocketV6.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 0 });
            SocketV6.BeginReceive(bufferV6, 0, bufferV6.Length, SocketFlags.None, new AsyncCallback(ReceiveCallbackV6), null);
        }

        void ReceiveCallbackV4(IAsyncResult ar)
        {
            try
            {
                var bytesReceived = SocketV4.EndReceive(ar);
                if (bytesReceived > 0)
                {
                    // Prepend fake ethernet header
                    byte[] packetData = new byte[FakeEthernetHeaderV4.Length + bytesReceived];
                    Array.Copy(FakeEthernetHeaderV4, packetData, FakeEthernetHeaderV4.Length);
                    Array.Copy(bufferV4, 0, packetData, FakeEthernetHeaderV4.Length, bytesReceived);
                    
                    Captured?.Invoke(this, new PacketCapturedEventArgs(Packet.ParsePacket(LinkLayers.Ethernet, packetData)));
                }
            }
            catch { }
            finally
            {
                try
                {
                    if (SocketV4 != null)
                    {
                        SocketV4.BeginReceive(bufferV4, 0, bufferV4.Length, SocketFlags.None, new AsyncCallback(ReceiveCallbackV4), null);
                    }
                }
                catch { }
            }
        }

        void ReceiveCallbackV6(IAsyncResult ar)
        {
            try
            {
                var bytesReceived = SocketV6.EndReceive(ar);
                if (bytesReceived > 0)
                {
                    // Prepend fake ethernet header
                    byte[] packetData = new byte[FakeEthernetHeaderV6.Length + bytesReceived];
                    Array.Copy(FakeEthernetHeaderV6, packetData, FakeEthernetHeaderV6.Length);
                    Array.Copy(bufferV6, 0, packetData, FakeEthernetHeaderV6.Length, bytesReceived);

                    Captured?.Invoke(this, new PacketCapturedEventArgs(Packet.ParsePacket(LinkLayers.Ethernet, packetData)));
                }
            }
            catch
            {

            }
            finally
            {
                try
                {
                    if (SocketV6 != null)
                    {
                        SocketV6.BeginReceive(bufferV6, 0, bufferV6.Length, SocketFlags.None, new AsyncCallback(ReceiveCallbackV6), null);
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            SocketV4.Close();
            SocketV4.Dispose();
            SocketV6.Close();
            SocketV6.Dispose();
        }
    }
}
