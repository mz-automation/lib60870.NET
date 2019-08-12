// Client example to demonstrate file upload to server
using System;
using System.IO;
using System.Threading;
using lib60870;
using lib60870.CS101;
using lib60870.CS104;

namespace cs104_client_file_upload
{
    public class SimpleFile : TransparentFile
    {
        private AutoResetEvent ready = new AutoResetEvent (false);

        public SimpleFile (int ca, int ioa, NameOfFile nof)
            : base (ca, ioa, nof)
        {
        }

        public override void TransferComplete (bool success)
        {
            Console.WriteLine ("Transfer complete: " + success.ToString ());
            ready.Set ();
        }

        public void WaitUntilTransferIsComplete ()
        {
            ready.WaitOne ();
        }
    }

    class MainClass
    {
        public static void Main (string [] args)
        {
            string hostname = "127.0.0.1";
            string filename = null;
            int fileCa = 1;
            int fileIoa = 30001;

            if (args.Length == 0) {
                Console.WriteLine ("upload <hostname> <filename> [CA] [IOA]");
            } else {
                for (int i = 0; i < args.Length; i++) {
                    if (i == 0)
                        hostname = args [i];
                    if (i == 1)
                        filename = args [i];
                    if (i == 2)
                        Int32.TryParse(args[i], out fileCa);
                    if (i == 3)
                        Int32.TryParse(args[i], out fileIoa);
                }

            }

            Console.WriteLine ("Using lib60870.NET version " + LibraryCommon.GetLibraryVersionString ());

            Connection con = new Connection (hostname);

            con.Connect ();

            SimpleFile file = new SimpleFile (fileCa, fileIoa, NameOfFile.TRANSPARENT_FILE);

            if (filename != null) {
                file.AddSection (File.ReadAllBytes (filename));
            } else {
                byte [] fileData = new byte [1025];

                for (int i = 0; i < 1025; i++)
                    fileData [i] = (byte)(i + 1);

                file.AddSection (fileData);
            }

            con.SendFile (fileCa, fileIoa, (NameOfFile) 12, file);

            file.WaitUntilTransferIsComplete ();

            con.Close ();

            Console.WriteLine ("Press any key to terminate...");
            Console.ReadKey ();
        }
    }
}
