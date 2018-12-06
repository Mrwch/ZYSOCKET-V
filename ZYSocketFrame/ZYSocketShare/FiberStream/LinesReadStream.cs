﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;


namespace ZYSocket.FiberStream
{
    public class LinesReadStream : Stream, IFiberReadStream
    {
        private readonly object lockobj = new object();

        private readonly Pipes Pipes;

        private readonly byte[] data;

        private int offset;

        private readonly int len;

        private long wrlen;

        private long position;

        private bool is_canceled;

        private StreamInitAwaiter InitAwaiter;

        private bool is_sync;
        public bool IsSync { get => is_sync; set => is_sync = value; }
        private readonly byte[] numericbytes = new byte[8];

        public byte[]  Numericbytes { get => numericbytes; }

        public LinesReadStream(int length = 4096)
        {
            Pipes = new Pipes();
            data = new byte[length];
            len = length;
            is_sync = false;
        }


        public  PipeFilberAwaiter Advance(int len, CancellationToken cancellationTokenSource = default)
        {
            wrlen = len;
            return  Pipes.Advance(len, cancellationTokenSource);
        }

        public PipeFilberAwaiter ReadCanceled()
        {
            return Pipes.ReadCanceled();
        }


        public Memory<byte> GetMemory(int inithnit)
        {
            return new Memory<byte>(data, offset, get_have_length());
        }

        public ArraySegment<byte> GetArray(int inithnit)
        {
            return new ArraySegment<byte>(data, offset, get_have_length());
        }

        private int get_have_length()
        {
            return len - offset;
        }

        private long have_current_length()
        {
            return wrlen - position;
        }



        public async Task Check()
        {
            if (position == wrlen)
            {
                
                for (; ; )
                {
                    var res = await Pipes.Need(0);
                  
                    if (res.IsCanceled)
                    {
                        is_canceled = true;
                        break;
                    }
                    else
                    {
                        if (res.ByteLength == 0)
                            continue;
                        else
                        {
                            position = 0;
                            break;
                        }
                    }
                }

            }

        }

        public void Reset()
        {
            if (InitAwaiter != null)
                InitAwaiter.Reset();
            position = 0;
            wrlen = 0;
            offset = 0;
            is_canceled = false;
            Pipes.ResetFilber();
        }

        public bool IsCanceled { get => is_canceled; }


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => wrlen;

        public override long Position { get => position; set => position = value; }



        public override void Flush()
        {

        }



        public StreamInitAwaiter WaitStreamInit()
        {
            if (InitAwaiter != null)
                return InitAwaiter;
            else
            {
                InitAwaiter = new StreamInitAwaiter();
                return InitAwaiter;
            }
        }

        public void StreamInit()
        {
            if (InitAwaiter != null)
            {
                InitAwaiter.SetResult(true);
                InitAwaiter.Completed();
            }
            else
                throw new Exception("Please call first  WaitStreamInit");
        }


        public void UnStreamInit()
        {
            if (InitAwaiter != null)
            {
                InitAwaiter.SetResult(false);
                InitAwaiter.Completed();
            }
            else
                throw new Exception("Please call first  WaitStreamInit");
        }


     

        public override int Read(byte[] buffer, int offset, int count)
        {
            unsafe
            {
                if (is_canceled)
                    return 0;

                var cpbytes = wrlen - position;
                if (cpbytes == 0)
                    return 0;

                fixed (byte* source = &data[this.offset + position])
                fixed (byte* target = &buffer[offset])
                {
                   
                    if (cpbytes > count)                    
                        cpbytes = count;                    

                    Buffer.MemoryCopy(source, target, count, cpbytes);
                    position += cpbytes;

                    return (int)cpbytes;
                }
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken=default)
        {
            if (is_sync)
            {
                lock (lockobj)
                {
                    int size = Read(buffer, offset, count);
                    if (size == 0)
                    {
                        if (!Check().Wait(100))
                            position = 0;
                        size = Read(buffer, offset, count);
                    }
                    return size;
                }
            }
            else
            {

                int size = Read(buffer, offset, count);
                if (size == 0)
                {
                    await Check();
                    size = Read(buffer, offset, count);
                }
                return size;
            }
        }


        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {        
            var task = ReadAsync(buffer, offset, count);
            return TaskToApm.Begin(task, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskToApm.End<int>(asyncResult);
        }



        public  Memory<byte> ReadToBlockEnd()
        {

            if (is_canceled)
                return Memory<byte>.Empty;

            int count = (int)have_current_length();
            position = wrlen;
            return new Memory<byte>(data, offset, count);

        }

        public ArraySegment<byte> ReadToBlockArrayEnd()
        {
            if (is_canceled)
                return default;

            int count = (int)have_current_length();
            position = wrlen;
            return new ArraySegment<byte>(data, offset, count);

        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
