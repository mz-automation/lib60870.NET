using System;

namespace lib60870
{
	public class LibraryCommon
	{
		public const int VERSION_MAJOR = 1;
		public const int VERSION_MINOR = 0;
		public const int VERSION_PATCH = 0;

		public static string GetLibraryVersionString()
		{
			return "" + VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_PATCH;
		}
	}

	/// <summary>
	/// Raw message handler. Can be used to access the raw message.
	/// Returns true when message should be handled by the protocol stack, false, otherwise.
	/// </summary>
	public delegate bool RawMessageHandler (object parameter, byte[] message, int messageSize);
}

