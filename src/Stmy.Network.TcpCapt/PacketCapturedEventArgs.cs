using System;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    public class PacketCapturedEventArgs : EventArgs
    {
        public Packet Packet { get; }

        public PacketCapturedEventArgs(Packet packet)
        {
            Packet = packet;
        }
    }
}
