/*
 *  Copyright 2018 MZ Automation GmbH
 *
 *  This file is part of lib60870.NET
 *
 *  lib60870.NET is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  lib60870.NET is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with lib60870.NET.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  See COPYING file for the complete license text.
 */


using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace lib60870.linklayer
{
	public class TcpClientVirtualSerialPort : Stream
	{
		private int readTimeout = 0;

		private bool debugOutput = false;
		private bool running = false;
		private bool connected = false;

		private string hostname;
		private int tcpPort;

		Socket conSocket = null;
		Stream socketStream = null;
		Thread connectionThread;

		private int connectTimeoutInMs = 1000;
		private int waitRetryConnect = 1000;

		private void DebugLog(string msg)
		{
			if (debugOutput) {
				Console.Write ("CS101 TCP link layer: ");
				Console.WriteLine (msg);
			}
		}

		public bool DebugOutput {
			get {
				return this.debugOutput;
			}
			set {
				debugOutput = value;
			}
		}

		public TcpClientVirtualSerialPort(String hostname, int tcpPort = 2404)
		{
			this.hostname = hostname;
			this.tcpPort = tcpPort;
		}

		private void ConnectSocketWithTimeout()
		{
			IPAddress ipAddress;
			IPEndPoint remoteEP;

			try
			{
				ipAddress = IPAddress.Parse(hostname);
				remoteEP = new IPEndPoint(ipAddress, tcpPort);
			}
			catch (Exception)
			{
				throw new SocketException(87); // wrong argument
			}

			// Create a TCP/IP  socket.
			conSocket = new Socket(AddressFamily.InterNetwork,
				SocketType.Stream, ProtocolType.Tcp);

			var result = conSocket.BeginConnect(remoteEP, null, null);

			bool success = result.AsyncWaitHandle.WaitOne(connectTimeoutInMs, true);
			if (success)
			{
				try
				{
					conSocket.EndConnect(result);
					conSocket.NoDelay = true;
				}
				catch (ObjectDisposedException)
				{
					conSocket = null;

					DebugLog("ObjectDisposedException -> Connect canceled");

					throw new SocketException(995); // WSA_OPERATION_ABORTED
				}
			}
			else
			{
				conSocket.Close();
				conSocket = null;

				throw new SocketException(10060); // Connection timed out (WSAETIMEDOUT)
			}
		}

		private void ConnectionThread()
		{
			running = true;

			DebugLog("Starting connection thread");

			while (running) {

				try {
					DebugLog("Connecting to " + hostname + ":" + tcpPort);

					ConnectSocketWithTimeout();

					socketStream = new NetworkStream(conSocket);

					connected = true;

					while (connected) {

						if (conSocket.Connected == false)
							break;

						Thread.Sleep(10);
					}

					connected = false;
					socketStream = null;
					conSocket.Close();
					conSocket = null;

				} catch (SocketException) {
					connected = false;
					socketStream = null;
					conSocket = null;
				}
					
				Thread.Sleep (waitRetryConnect);
			}
		}

		public void Start() 
		{
			if (running == false) {
				connectionThread = new Thread (ConnectionThread);

				connectionThread.Start ();
			}
		}


		public void Stop()
		{
			if (running == true) {
				running = false;

				if (conSocket != null)
					conSocket.Close ();

				connectionThread.Join ();
			}
		}


		/*************************
		 * Stream implementation 
		 */

		public override int Read (byte[] buffer, int offset, int count)
		{
			if (socketStream != null) {

				if (conSocket.Poll (ReadTimeout, SelectMode.SelectRead)) {
					if (connected)
						return socketStream.Read (buffer, offset, count);
					else
						return 0;
				} else
					return 0;
			}
			else
				return 0;
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			if (socketStream != null) {
				try {
					socketStream.Write (buffer, offset, count);
				}
				catch (IOException) {
					connected = false;
				}
			}
		}

		public override bool CanRead {
			get {
				return true;
			}
		}

		public override bool CanSeek {
			get {
				return false;
			}
		}

		public override bool CanTimeout {
			get {
				return true;
			}
		}

		public override bool CanWrite {
			get {
				return true;
			}
		}

		public override long Length {
			get {
				throw new NotImplementedException ();
			}
		}

		public override long Position {
			get {
				throw new NotImplementedException ();
			}
			set {
				throw new NotImplementedException ();
			}
		}

		public override int ReadTimeout {
			get {
				return readTimeout;
			}
			set {
				readTimeout = value;
			}
		}

		public override int WriteTimeout {
			get {
				return base.WriteTimeout;
			}
			set {
				base.WriteTimeout = value;
			}
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotImplementedException ();
		}

		public override void Flush ()
		{
			if (socketStream != null)
				socketStream.Flush ();
		}

		public override void SetLength (long value)
		{
			throw new NotImplementedException ();
		}
	}
}
