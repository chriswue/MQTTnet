﻿using System;
using System.Text;
using MQTTnet.Protocol;

namespace MQTTnet.Serializer
{
    /// <summary>
    /// This is a custom implementation of a memory stream which provides only MQTTnet relevant features.
    /// The goal is to avoid lots of argument checks like in the original stream. The growth rule is the
    /// same as for the original MemoryStream in .net. Also this implementation allows accessing the internal
    /// buffer for all platforms and .net framework versions (which is not available at the regular MemoryStream).
    /// </summary>
    public class MqttPacketWriter
    {
        private byte[] _buffer = new byte[128];

        private int _position;

        public int Length { get; private set; }

        public static byte BuildFixedHeader(MqttControlPacketType packetType, byte flags = 0)
        {
            var fixedHeader = (int)packetType << 4;
            fixedHeader |= flags;
            return (byte)fixedHeader;
        }

        public static ArraySegment<byte> EncodeRemainingLength(int length)
        {
            // write the encoded remaining length right aligned on the 4 byte buffer
            if (length <= 0)
            {
                return new ArraySegment<byte>(new byte[1], 0, 1);
            }

            var buffer = new byte[4];
            var bufferOffset = 0;

            // Algorithm taken from http://docs.oasis-open.org/mqtt/mqtt/v3.1.1/os/mqtt-v3.1.1-os.html.
            var x = length;
            do
            {
                var encodedByte = x % 128;
                x = x / 128;
                if (x > 0)
                {
                    encodedByte = encodedByte | 128;
                }

                buffer[bufferOffset] = (byte)encodedByte;
                bufferOffset++;
            } while (x > 0);

            return new ArraySegment<byte>(buffer, 0, bufferOffset);
        }

        public void WriteWithLengthPrefix(string value)
        {
            WriteWithLengthPrefix(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        public void WriteWithLengthPrefix(byte[] value)
        {
            EnsureAdditionalCapacity(value.Length + 2);

            Write((ushort)value.Length);
            Write(value, 0, value.Length);
        }
        
        public void Write(byte @byte)
        {
            EnsureAdditionalCapacity(1);

            _buffer[_position] = @byte;
            IncreasePostition(1);
        }

        public void Write(ushort value)
        {
            EnsureAdditionalCapacity(2);

            _buffer[_position] = (byte)(value >> 8);
            IncreasePostition(1);
            _buffer[_position] = (byte)value;
            IncreasePostition(1);
        }

        public void Write(byte[] array, int offset, int count)
        {
            EnsureAdditionalCapacity(count);

            Array.Copy(array, offset, _buffer, _position, count);
            IncreasePostition(count);
        }

        public void Reset()
        {
            Length = 0;
        }

        public void Seek(int offset)
        {
            EnsureCapacity(offset);
            _position = offset;
        }

        public byte[] GetBuffer()
        {
            return _buffer;
        }

        public void FreeBuffer()
        {
            // This method frees the used memory by shrinking the buffer. This is required because the buffer
            // is used across several messages. In general this is not a big issue because subsequent Ping packages
            // have the same size but a very big publish package with 100 MB of payload will increase the buffer 
            // a lot and the size will never reduced. So this method tries to find a size which can be held in 
            // memory for a long time without causing troubles.

            if (_buffer.Length < 4096)
            {
                return;
            }

            Array.Resize(ref _buffer, 4096);
        }

        private void EnsureAdditionalCapacity(int additionalCapacity)
        {
            var freeSpace = _buffer.Length - _position;
            if (freeSpace >= additionalCapacity)
            {
                return;
            }

            EnsureCapacity(additionalCapacity - freeSpace);
        }

        private void EnsureCapacity(int capacity)
        {
            if (_buffer.Length >= capacity)
            {
                return;
            }

            var newBufferLength = _buffer.Length;
            while (newBufferLength < capacity)
            {
                newBufferLength *= 2;
            }

            Array.Resize(ref _buffer, newBufferLength);
        }

        private void IncreasePostition(int length)
        {
            _position += length;
            if (_position > Length)
            {
                Length = _position;
            }
        }
    }
}