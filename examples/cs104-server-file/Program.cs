using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using lib60870;
using lib60870.CS101;
using lib60870.CS104;
using System.Collections.Generic;

namespace cs104_server_file
{
	public class SimpleFile : TransparentFile
	{
		public SimpleFile (int ca, int ioa, NameOfFile nof)
			: base (ca, ioa, nof)
		{
		}

		public override void TransferComplete (bool success)
		{
			Console.WriteLine ("Transfer complete: " + success.ToString());
		}
	}
	
	class MainClass
	{
		private static bool interrogationHandler(object parameter, IMasterConnection connection, ASDU asdu, byte qoi)
		{
			Console.WriteLine ("Interrogation for group " + qoi);

			ApplicationLayerParameters cp = connection.GetApplicationLayerParameters ();

			connection.SendACT_CON (asdu, false);

			// send information objects
			ASDU newAsdu = new ASDU(cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, false);

			newAsdu.AddInformationObject (new MeasuredValueScaled (100, -1, new QualityDescriptor ()));

			newAsdu.AddInformationObject (new MeasuredValueScaled (101, 23, new QualityDescriptor ()));

			newAsdu.AddInformationObject (new MeasuredValueScaled (102, 2300, new QualityDescriptor ()));

			connection.SendASDU (newAsdu);

			newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 3, 1, false);

			newAsdu.AddInformationObject(new MeasuredValueScaledWithCP56Time2a(103, 3456, new QualityDescriptor (), new CP56Time2a(DateTime.Now)));

			connection.SendASDU (newAsdu);

			newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, false);

			newAsdu.AddInformationObject (new SinglePointWithCP56Time2a (104, true, new QualityDescriptor (), new CP56Time2a (DateTime.Now)));

			connection.SendASDU (newAsdu);

			// send sequence of information objects
			newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, true);

			newAsdu.AddInformationObject (new SinglePointInformation (200, true, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (201, false, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (202, true, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (203, false, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (204, true, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (205, false, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (206, true, new QualityDescriptor ()));
			newAsdu.AddInformationObject (new SinglePointInformation (207, false, new QualityDescriptor ()));

			connection.SendASDU (newAsdu);

			newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, true);

			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (300, -1.0f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (301, -0.5f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (302, -0.1f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (303, .0f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (304, 0.1f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (305, 0.2f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (306, 0.5f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (307, 0.7f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (308, 0.99f));
			newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (309, 1f));

			connection.SendASDU (newAsdu);

			connection.SendACT_TERM (asdu);

			return true;
		}

		private static bool asduHandler(object parameter, IMasterConnection connection, ASDU asdu)
		{
			
			if (asdu.TypeId == TypeID.C_SC_NA_1) {
				Console.WriteLine ("Single command");

				SingleCommand sc = (SingleCommand)asdu.GetElement (0);

				Console.WriteLine (sc.ToString ());
			} else if (asdu.TypeId == TypeID.M_EI_NA_1) {
				Console.WriteLine ("End of initialization received");
			}
			else if (asdu.TypeId == TypeID.F_DR_TA_1) {
			
				Console.WriteLine ("Received file directory");
			}
			else if (asdu.TypeId == TypeID.C_CS_NA_1){
				

				ClockSynchronizationCommand qsc = (ClockSynchronizationCommand)asdu.GetElement (0);

				Console.WriteLine ("Received clock sync command with time " + qsc.NewTime.ToString());
			}

			return true;
		}

		public static void Main (string[] args)
		{
			bool running = true;

			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true;
				running = false;
			};

			Server server = new Server ();

			server.DebugOutput = true;

			server.MaxQueueSize = 10;

			server.SetInterrogationHandler (interrogationHandler, null);

			server.SetASDUHandler (asduHandler, null);

			server.Start ();

			SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

			byte[] fileData = new byte[1025];

			for (int i = 0; i < 1025; i++)
				fileData [i] = (byte)(i + 1);

			file.AddSection (fileData);

			SimpleFile file2 = new SimpleFile (1, 30001, NameOfFile.TRANSPARENT_FILE);
			file2.AddSection (fileData);

			server.GetAvailableFiles().AddFile (file);
			server.GetAvailableFiles().AddFile (file2);

            ASDU newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.INITIALIZED, false, false, 0, 1, false);
            EndOfInitialization eoi = new EndOfInitialization(0);
            newAsdu.AddInformationObject(eoi);
            server.EnqueueASDU(newAsdu);

			while (running) {
				Thread.Sleep(100);
			}

			Console.WriteLine ("Stop server");
			server.Stop ();
		}
	}
}
