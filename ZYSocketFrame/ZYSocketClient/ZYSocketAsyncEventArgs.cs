﻿using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZYSocket.FiberStream;
using ZYSocket.Share;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace ZYSocket.Client
{

    public class ZYSocketAsyncEventArgs : SocketAsyncEventArgs, ISockAsyncEventAsClient
    {
      

        private readonly IFiberReadStream RStream;

        private readonly IFiberWriteStream WStream;
      
        private bool IsInit = false;

        private readonly MemoryPool<byte> MemoryPool;

        public  bool IsLittleEndian { get; private set; }
        public  Encoding Encoding { get; private set; }
        public ISend SendImplemented { get;  private set; }
        public IAsyncSend AsyncSendImplemented { get; private set; }    


        private int _check_thread = 0;     

        public int Add_check()
        {
            _check_thread++;
            return _check_thread;
        }

        public void Reset_check()
        {
            _check_thread = 0;
        }

        public ZYSocketAsyncEventArgs(IFiberReadStream r_stream, IFiberWriteStream w_stream, ISend send,IAsyncSend asyncsend, MemoryPool<byte> memoryPool, Encoding encoding, bool isLittleEndian=false)
        {
            this.MemoryPool = memoryPool;
            this.RStream = r_stream;
            this.WStream = w_stream;
            this.Encoding = encoding;
            base.Completed += ZYSocketAsyncEventArgs_Completed;
            IsLittleEndian = isLittleEndian;
            SendImplemented = send;
            AsyncSendImplemented = asyncsend;
        }

        private void ZYSocketAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            if(Completed!=null)
                this.Completed(sender, this);
        }

        public new event EventHandler<ZYSocketAsyncEventArgs> Completed;

        public async ValueTask<IFiberRw> GetFiberRw(Func<Stream, Stream, (Stream, Stream)> init = null)
        {
            if (await RStream.WaitStreamInit())
            {
                return new FiberRw<object>(this, RStream, WStream, MemoryPool, Encoding, IsLittleEndian, init: init);
            }
            else
                return null;
        }

        public async ValueTask<IFiberRw<T>> GetFiberRw<T>(Func<Stream, Stream, (Stream, Stream)> init = null) where T:class
        {
            if (await RStream.WaitStreamInit())
            {
                return new FiberRw<T>(this, RStream, WStream, MemoryPool, Encoding, IsLittleEndian,init:init);
            }
            else
                return null;
        }

        public async ValueTask<IFiberRw> GetFiberRwSSL(X509Certificate certificate_client,string targethost, Func<Stream, Stream, (Stream, Stream)> init = null)
        {
            if (await RStream.WaitStreamInit())
            {
                var mergestream = new MergeStream(RStream, WStream);
                mergestream.IsSync = true;
                var sslstream = new SslStream(mergestream, false, (sender, certificate, chain, errors) => true,
                (sender, host, certificates, certificate, issuers) => certificate_client);

                try
                {
                    await sslstream.AuthenticateAsClientAsync(targethost);
                }
                catch
                {
                    return null;
                }

                mergestream.IsSync = false;
                return new FiberRw<object>(this, RStream, WStream, MemoryPool, Encoding, IsLittleEndian, sslstream, sslstream, init: init);
            }
            else
                return null;
        }

        public async ValueTask<IFiberRw<T>> GetFiberRwSSL<T>(X509Certificate certificate_client, string targethost, Func<Stream, Stream, (Stream, Stream)> init = null) where T : class
        {
            if (await RStream.WaitStreamInit())
            {
                var mergestream = new MergeStream(RStream, WStream);
                mergestream.IsSync = true;
                var sslstream = new SslStream(mergestream, false, (sender, certificate, chain, errors) => true,
                (sender, host, certificates, certificate, issuers) => certificate_client);
                try
                {
                    await sslstream.AuthenticateAsClientAsync(targethost);
                }
                catch
                {
                    return null;
                }
                mergestream.IsSync = false;
                return new FiberRw<T>(this, RStream, WStream, MemoryPool, Encoding, IsLittleEndian, sslstream, sslstream, init: init);
            }
            else
                return null;
        }


        public void StreamInit()
        {
            if (!IsInit)
            {
                IsInit = true;
                RStream.StreamInit();
            }
        }



        public void SetBuffer(int inthint)
        {           
            var mem = RStream.GetArray(inthint);
            base.SetBuffer(mem.Array, mem.Offset, mem.Count);
        }

        public void Reset()
        {
            base.SetBuffer(null, 0, 0);
            IsInit = false;
            RStream.Reset();
            WStream.Close();
            this.AcceptSocket = null;            
        }

        public PipeFilberAwaiter Advance(int bytesTransferred)
        {
            return RStream.Advance(bytesTransferred);
        }

        public PipeFilberAwaiter Advance()
        {
            return RStream.Advance(BytesTransferred);
        }

        public PipeFilberAwaiter ReadCanceled()
        {
            return RStream.ReadCanceled();
        }

     
    }

  
}
