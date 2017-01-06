using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    public class TcpStream : Stream
    {
        uint nextSeq;
        bool closed;
        bool first;
        bool waiting;
        bool disposed;
        readonly SortedDictionary<uint, TcpPacket> fragments;
        int blockPos;
        readonly Queue<byte[]> buffer;
        readonly AutoResetEvent timeoutEvent;
        readonly object lockObj = new object();

        internal int? FragmentTimeout { get; set; }
        public event EventHandler TimedOut;

        public TcpStream()
        {
            nextSeq = 0;
            closed = false;
            first = true;
            waiting = false;
            disposed = false;
            fragments = new SortedDictionary<uint, TcpPacket>();
            blockPos = 0;
            buffer = new Queue<byte[]>();
            timeoutEvent = new AutoResetEvent(false);
        }

        internal void Process(TcpPacket packet, bool useChecksum = true)
        {
            if (packet == null) { throw new ArgumentNullException(nameof(packet)); }

            if (useChecksum && !packet.ValidTCPChecksum)
            {
                return;
            }

            lock (lockObj)
            {
                // Set initial sequence number
                if (first)
                {
                    nextSeq = packet.SequenceNumber;
                    first = false;
                }

                if (packet.Fin) { closed = true; }

                // Ignore payload-less packet (such as ACK packet)
                if (packet.PayloadData == null || packet.PayloadData.Length == 0)
                {
                    return;
                }

                var offset = CalculateOffset(packet);

                if (offset < 0) // retransmitted
                {
                    ProcessRetransmissionPacket(packet, offset);
                }
                else if (offset > 0) // fragmented
                {
                    ProcessFragmentedPacket(packet);
                }
                else
                {
                    ProcessRegularPacket(packet);
                }
            }
        }

        int CalculateOffset(TcpPacket packet)
        {
            const uint Threshold = uint.MaxValue / 2;

            if (packet.SequenceNumber < nextSeq)
            {
                if (nextSeq - packet.SequenceNumber > Threshold)
                {
                    return (int)(uint.MaxValue - nextSeq + packet.SequenceNumber);
                }
                else
                {
                    return (int)-(nextSeq - packet.SequenceNumber);
                }
            }
            else
            {
                if (packet.SequenceNumber - nextSeq > Threshold)
                {
                    return -(int)(uint.MaxValue - packet.SequenceNumber + nextSeq);
                }
                else
                {
                    return (int)(packet.SequenceNumber - nextSeq);
                }
            }
        }

        void ProcessRetransmissionPacket(TcpPacket packet, int offset)
        {
            // Read payload when packet contains subsequent data
            if (offset + packet.PayloadData.Length > 0)
            {
                Trace.WriteLine($"Retr. with subsequent payload: {packet.SequenceNumber}");
                ReadPayload(packet, -offset);
            }
            else
            {
                Trace.WriteLine($"Ignored due to retr.: {packet.SequenceNumber}");
            }
        }

        void ProcessFragmentedPacket(TcpPacket packet)
        {
            if (!fragments.ContainsKey(packet.SequenceNumber))
            {
                Trace.WriteLine($"Storing fragmented packet: {packet.SequenceNumber}");
                fragments.Add(packet.SequenceNumber, packet);
            }
            else if (packet.PayloadData.Length > fragments[packet.SequenceNumber].PayloadData.Length)
            {
                Trace.WriteLine($"Replacing fragmented packet: {packet.SequenceNumber}");
                fragments[packet.SequenceNumber] = packet;
            }
            else
            {
                Trace.WriteLine($"Discarding fragmented packet: {packet.SequenceNumber}");
            }

            if (!waiting)
            {
                waiting = true;
                Task.Run(() => WaitForNextPacket());
            }
        }

        void WaitForNextPacket()
        {
            Trace.WriteLine($"Waiting for packet: {nextSeq}");
            timeoutEvent.Reset();
            if (!timeoutEvent.WaitOne(FragmentTimeout ?? 1000)) // TODO: Set timeout based on RTT
            {
                Trace.WriteLine($"Timed out for: {nextSeq}");

                TimedOut?.Invoke(this, EventArgs.Empty);

                lock (lockObj)
                {
                    var nextPacket = ConsumeFragmentOrNull(fragments.First().Key);
                    nextSeq = nextPacket.SequenceNumber;
                    ReadPayload(nextPacket);
                }

                waiting = false;
            }
            else
            {
                Trace.WriteLine($"Waiting cancelled");
            }
        }

        void ProcessRegularPacket(TcpPacket packet)
        {
            timeoutEvent.Set();

            Trace.WriteLine($"Regular packet: {nextSeq}");
            ReadPayload(packet);
        }

        void ReadPayload(TcpPacket packet, int bytesToSkip = 0)
        {
            var cur = packet;
            while (cur != null && cur.SequenceNumber + bytesToSkip == nextSeq)
            {
                var data = cur.PayloadData;
                nextSeq = unchecked(nextSeq + (uint)data.Length - (uint)bytesToSkip);

                if (bytesToSkip > 0)
                {
                    data = new byte[data.Length - bytesToSkip];
                    Array.Copy(cur.PayloadData, bytesToSkip, data, 0, data.Length);
                }

                lock (buffer)
                {
                    buffer.Enqueue(data);
                }

                cur = ConsumeFragmentOrNull(nextSeq);
            }
        }

        TcpPacket ConsumeFragmentOrNull(uint seq)
        {
            TcpPacket packet;
            if (fragments.TryGetValue(seq, out packet))
            {
                Trace.WriteLine($"Consuming: {seq}");
                fragments.Remove(seq);
            }

            return packet;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            lock (lockObj)
            {
                timeoutEvent.Dispose();
                fragments.Clear();
                lock (buffer)
                {
                    buffer.Clear();
                }
            }
            disposed = true;
        }

        #region System.IO.Stream implementation

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) { throw new ArgumentNullException(nameof(buffer)); }
            if (offset < 0) { throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} is negative."); }
            if (count < 0) { throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(count)} is negative."); }
            if (offset + count > buffer.Length) { throw new ArgumentException($"The sum of {nameof(offset)} and {nameof(count)} is larger than the buffer length."); }
            if (disposed) { throw new ObjectDisposedException(GetType().FullName); }

            int bytesReaded = 0;

            lock (this.buffer)
            {
                while (this.buffer.Any())
                {
                    var block = this.buffer.Peek();
                    if (count >= block.Length - blockPos)
                    {
                        var bytesToRead = block.Length - blockPos;
                        Array.Copy(block, blockPos, buffer, offset, bytesToRead);
                        bytesReaded += bytesToRead;
                        offset += bytesToRead;
                        blockPos = 0;
                        this.buffer.Dequeue();
                    }
                    else
                    {
                        Array.Copy(block, blockPos, buffer, offset, count);
                        bytesReaded += count;
                        blockPos += count;
                    }
                }
            }

            return bytesReaded;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
