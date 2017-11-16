/*
 *  ApplicationLayerParameters.cs
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

namespace lib60870.CS104
{
	/// <summary>
	/// Parameters for the CS 104 APCI (Application Protocol Control Information)
	/// </summary>
	public class APCIParameters
	{

		private int k = 12; /* number of unconfirmed APDUs in I format
		                (range: 1 .. 32767 (2^15 - 1) - sender will
		                stop transmission after k unconfirmed I messages */

		private int w = 8; /* number of unconfirmed APDUs in I format 
						      (range: 1 .. 32767 (2^15 - 1) - receiver
						      will confirm latest after w messages */

		private int t0 = 10; /* connection establishment (in s) */

		private int t1 = 15; /* timeout for transmitted APDUs in I/U format (in s)
		                   when timeout elapsed without confirmation the connection
		                   will be closed */

		private int t2 = 10; /* timeout to confirm messages (in s) */

		private int t3 = 20; /* time until test telegrams in case of idle connection */


		public APCIParameters ()
		{
		}

		public APCIParameters Clone() {
			APCIParameters copy = new APCIParameters();

			copy.k = k;
			copy.w = w;
			copy.t0 = t0;
			copy.t1 = t1;
			copy.t2 = t2;
			copy.t3 = t3;

			return copy;
		}

		public int K {
			get {
				return this.k;
			}
			set {
				k = value;
			}
		}

		public int W {
			get {
				return this.w;
			}
			set {
				w = value;
			}
		}

		public int T0 {
			get {
				return this.t0;
			}
			set {
				t0 = value;
			}
		}

		public int T1 {
			get {
				return this.t1;
			}
			set {
				t1 = value;
			}
		}

		public int T2 {
			get {
				return this.t2;
			}
			set {
				t2 = value;
			}
		}

		public int T3 {
			get {
				return this.t3;
			}
			set {
				t3 = value;
			}
		}
	}
}