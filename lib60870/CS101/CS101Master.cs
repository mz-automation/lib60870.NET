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
using System.IO.Ports;
using System.Collections.Generic;

using lib60870.linklayer;
using System.Threading;

namespace lib60870.CS101
{
	public abstract class CS101Master : Master
	{
		protected Thread workerThread = null;

		internal LinkLayer linkLayer = null;

		internal FileClient fileClient = null;

		protected SerialPort port;
		protected bool running = false;

		private void ReceiveMessageLoop()
		{
			running = true;

			while (running) {
				Run ();

				Thread.Sleep (1);
			}
		}

		/// <summary>
		/// Run the protocol state machines a single time.
		/// Alternative to Start/Stop when no background thread should be used
		/// Has to be called frequently
		/// </summary>
		public void Run()
		{
			linkLayer.Run ();

			if (fileClient != null)
				fileClient.HandleFileService ();
		}

		/// <summary>
		/// Start a background thread running the master
		/// </summary>
		public void Start()
		{
			if (port.IsOpen == false)
				port.Open ();

			port.DiscardInBuffer ();

			workerThread = new Thread(ReceiveMessageLoop);

			workerThread.Start();
		}

		/// <summary>
		/// Stop the background thread
		/// </summary>
		public void Stop()
		{
			if (running)
			{
				running = false;

				if (workerThread != null)
					workerThread.Join();
			}
		}
	}

}