/*
 *  Master.cs
 *
 *  Copyright 2017 MZ Automation GmbH
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

namespace lib60870.CS101
{
	/// <summary>
	/// Handler that is called when a new ASDU is received
	/// </summary>
	public delegate bool ASDUReceivedHandler (object parameter, ASDU asdu);

	public abstract class Master {

		protected bool debugOutput;

		public bool DebugOutput {
			get {
				return this.debugOutput;
			}
			set {
				debugOutput = value;
			}
		}

		protected ASDUReceivedHandler asduReceivedHandler = null;
		protected object asduReceivedHandlerParameter = null;

		public void SetASDUReceivedHandler(ASDUReceivedHandler handler, object parameter)
		{
			asduReceivedHandler = handler;
			asduReceivedHandlerParameter = parameter;
		}

		/// <summary>
		/// Sends the interrogation command.
		/// </summary>
		/// <param name="cot">Cause of transmission</param>
		/// <param name="ca">Common address</param>
		/// <param name="qoi">Qualifier of interrogation (20 = station interrogation)</param>
		/// <exception cref="ConnectionException">description</exception>
		public abstract void SendInterrogationCommand(CauseOfTransmission cot, int ca, byte qoi);

		//TODO add other master related functions
	}

	/// <summary>
	/// ASDU received handler.
	/// </summary>
	public delegate bool SlaveASDUReceivedHandler (object parameter, int slaveAddress, ASDU asdu);

	public abstract class UnbalancedMaster {
		protected bool debugOutput;
		protected int slaveAddress = 0;

		public bool DebugOutput {
			get {
				return this.debugOutput;
			}
			set {
				debugOutput = value;
			}
		}

		protected SlaveASDUReceivedHandler asduReceivedHandler = null;
		protected object asduReceivedHandlerParameter = null;

		public void SetASDUReceivedHandler(SlaveASDUReceivedHandler handler, object parameter)
		{
			asduReceivedHandler = handler;
			asduReceivedHandlerParameter = parameter;
		}


		/// <summary>
		/// Sets the slave address for the next application layer message/service
		/// </summary>
		/// <param name="slaveAddress">Slave address.</param>
		public void UseSlaveAddress(int slaveAddress)
		{
			this.slaveAddress = slaveAddress;
		}


		/// <summary>
		/// Sends the interrogation command.
		/// </summary>
		/// <param name="cot">Cause of transmission</param>
		/// <param name="ca">Common address</param>
		/// <param name="qoi">Qualifier of interrogation (20 = station interrogation)</param>
		/// <exception cref="ConnectionException">description</exception>
		public abstract void SendInterrogationCommand(CauseOfTransmission cot, int ca, byte qoi);

	}

}

