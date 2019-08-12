using System;
using System.IO;
using System.Threading;

using lib60870;
using lib60870.CS101;
using lib60870.CS104;

namespace cs104_server_file
{
    /// <summary>
    /// Extend TransparentFile or implement IFileProvider to allow file downloads to the master
    /// </summary>
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

    /// <summary>
    /// Implement IFileReceiver to allow file uploads from the master
    /// </summary>
    public class MyReceiver : IFileReceiver
    {

        public byte [] recvBuffer;
        public int recvdBytes = 0;

        public MyReceiver (int bufferSize)
        {
            recvBuffer = new byte [bufferSize];
        }

        public void Finished (FileErrorCode result)
        {
            Console.WriteLine ("File download finished - code: " + result.ToString ());

            // now the valid file data it in the buffer. User can now handle the file data (e.g. store data in local file system)
            if (result == FileErrorCode.SUCCESS) {
                File.WriteAllBytes ("file_30001.dat", recvBuffer);
            }
        }

        public void SegmentReceived (byte sectionName, int offset, int size, byte [] data)
        {
            Array.Copy (data, 0, recvBuffer, recvdBytes, size);
            recvdBytes += size;
            Console.WriteLine ("File segment - sectionName: {0} offset: {1} size: {2}", sectionName, offset, size);
            for (int i = 0; i < size; i++) {
                Console.Write (" " + data [i]);
            }
            Console.WriteLine ();
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

                SingleCommand sc = (SingleCommand)asdu.GetElement (0);

                if (sc.ObjectAddress != 100) {
                    // Unkown IOA --> send negative confirmation
                    asdu.Cot = CauseOfTransmission.UNKNOWN_INFORMATION_OBJECT_ADDRESS;
                    asdu.IsNegative = true;
                    connection.SendASDU (asdu);
                } else {
                    // execute command

                    // send positive confirmation
                    connection.SendACT_CON (asdu, false);
                }

            }

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

            // Install a handler to allow file downloads (will be called when the master sends a file ready ASDU to anounce a file transfer)
            server.SetFileReadyHandler (delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) {

                if ((ca == 1) && (ioa == 30001) && (nof == NameOfFile.TRANSPARENT_FILE)) {

                    // Allow only files with a maximum of 5000 bytes
                    if (lengthOfFile > 5000) {
                        Console.WriteLine ("Deny file download. File too large");
                        return null;
                    } else {
                        Console.WriteLine ("Accept file download.");
                        return new MyReceiver (lengthOfFile);
                    }

                } else {
                    Console.WriteLine ("Deny file upload. Unknown file type.");
                    return null;
                }

            }, null);

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
