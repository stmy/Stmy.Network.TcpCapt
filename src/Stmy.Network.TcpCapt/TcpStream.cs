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
    /// <summary>
    /// Represents TCP data stream.
    /// </summary>
    /// <seealso cref="System.IO.Stream" />
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

        /// <summary>
        /// Milliseconds wait for missing fragmented TCP packets. Infinite when not set.
        /// </summary>
        internal int? FragmentTimeout { get; set; }

        /// <summary>
        /// Occurs when timed out for waiting missing fragmented TCP packets.
        /// </summary>
        public event EventHandler TimedOut;

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpStream"/> class.
        /// </summary>
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

        /// <summary>
        /// Process TCP packet and write data into stream when payload is available.
        /// </summary>
        /// <param name="packet">TCP packet to process.</param>
        /// <param name="useChecksum">
        ///     Validate packet with checksum when true. 
        ///     Do nothing when validation failed.
        /// </param>
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

        /// <summary>
        /// Calculate stream offset between packet's sequence number and expected next sequence number.
        /// </summary>
        /// <param name="packet">TCP packet to calculate stream offset.</param>
        /// <returns>
        ///     Stream offset of the packet.
        ///     Positive value represents when packet is fragmented and negative value represents
        ///     the packet is retransmitted.
        /// </returns>
        int CalculateOffset(TcpPacket packet)
        {
            long offset = packet.SequenceNumber - (long)nextSeq;

            if (Math.Abs(offset) <= int.MaxValue)
            {
                return (int)offset;
            }
            else // straddles uint boundary
            {
                if (offset < 0)
                {
                    return (int)(offset + uint.MaxValue);
                }
                else
                {
                    return (int)(offset - uint.MaxValue);
                }
            }
        }

        /// <summary>
        /// Process retransmitted TCP packet.
        /// </summary>
        /// <param name="packet">TCP packet to process.</param>
        /// <param name="offset">Offset value of the packet calculated with <see cref="CalculateOffset"/> method.</param>
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

        /// <summary>
        /// Process fragmented TCP packet.
        /// </summary>
        /// <param name="packet">TCP packet to process.</param>
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

        /// <summary>
        /// Wait for missing packet.
        /// </summary>
        void WaitForNextPacket()
        {
            Trace.WriteLine($"Waiting for packet: {nextSeq}");
            timeoutEvent.Reset();
            if (FragmentTimeout.HasValue
                    ? !timeoutEvent.WaitOne(FragmentTimeout.Value)
                    : !timeoutEvent.WaitOne()) // TODO: Set timeout based on RTT
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

        /// <summary>
        /// Process regular TCP packet.
        /// </summary>
        /// <param name="packet">TCP packet to process.</param>
        void ProcessRegularPacket(TcpPacket packet)
        {
            timeoutEvent.Set();

            Trace.WriteLine($"Regular packet: {nextSeq}");
            ReadPayload(packet);
        }

        /// <summary>
        /// Read payload of given packet and read subsequent packets if exists in the buffer.
        /// </summary>
        /// <param name="packet">TCP packet to read.</param>
        /// <param name="bytesToSkip">
        ///     Bytes to skip. 
        ///     Use this parameter when read payload of packets retransmitted with subsequent data.
        /// </param>
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

        /// <summary>
        /// Try to consume subsequent packet in the buffer.
        /// </summary>
        /// <param name="seq"></param>
        /// <returns>Subsequent packet when exist in the buffer, otherwise <c>null</c>.</returns>
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

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" /> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            lock (lockObj)
            {
                timeoutEvent.Set();
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

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="System.NotSupportedException"></exception>
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="System.NotSupportedException">
        /// </exception>
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <exception cref="System.NotSupportedException"></exception>
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="System.NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="System.NotSupportedException"></exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///     Reads a sequence of bytes from the current stream and advances the position 
        ///     within the stream by the number of bytes read.
        /// </summary>
        /// 
        /// <param name="buffer">
        ///     An array of bytes. 
        ///     When this method returns, the buffer contains the specified byte array with the values between 
        ///     <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        ///     The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.
        /// </param>
        /// <param name="count">
        ///     The maximum number of bytes to be read from the current stream.
        /// </param>
        /// 
        /// <returns>
        ///     The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// 
        /// <exception cref="System.ArgumentNullException">
        ///     Throws when <paramref name="buffer"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        ///     Throws when <paramref name="offset"/> parameter is negative and/or <paramref name="count"/> parameter is negative.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        ///     Throws when sum of <paramref name="offset"/> and <paramref name="count"/> is 
        ///     larger than the <paramref name="buffer"/>'s length.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">
        ///     Throws when the stream is disposed already.
        /// </exception>
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

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="System.NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
