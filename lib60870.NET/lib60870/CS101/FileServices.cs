/*
 *  FileObjects.cs
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
using System.Collections.Generic;

namespace lib60870.CS101
{

	public enum FileErrorCode {
		SUCCESS,
		TIMEOUT,
		FILE_NOT_READY,
		SECTION_NOT_READY,
		UNKNOWN_CA,
		UNKNOWN_IOA,
		UNKNOWN_SERVICE,
		PROTOCOL_ERROR,
		ABORTED_BY_REMOTE
	}

	public interface IFileReceiver {
		void Finished(FileErrorCode result);
		void SegmentReceived(byte sectionName, int offset, int size, byte[] data);
	}

	public interface IFileProvider {

		/// <summary>
		/// Returns the CA (Comman address) of the file
		/// </summary>
		/// <returns>The CA</returns>
		int GetCA();

		/// <summary>
		/// Returns the IOA (information object address of the file)
		/// </summary>
		/// <returns>The IOA</returns>
		int GetIOA();

		NameOfFile GetNameOfFile();

		DateTime GetFileDate();

		/// <summary>
		/// Gets the size of the file in bytes
		/// </summary>
		/// <returns>The file size in bytes</returns>
		int GetFileSize();

		/// <summary>
		/// Gets the size of a section in byzes
		/// </summary>
		/// <returns>The section size in bytes or -1 if the section does not exist</returns>
		/// <param name="sectionNumber">Number of section (starting with 0)</param>
		int GetSectionSize(int sectionNumber);

		/// <summary>
		/// Gets the segment data.
		/// </summary>
		/// <returns><c>true</c>, if segment data was gotten, <c>false</c> otherwise.</returns>
		/// <param name="sectionNumber">Section number.</param>
		/// <param name="offset">Offset.</param>
		/// <param name="segmentSize">Segment size.</param>
		/// <param name="segmentData">Segment data.</param>
		bool GetSegmentData(int sectionNumber, int offset, int segmentSize, byte[] segmentData);

		/// <summary>
		/// Indicates that the transfer is complete. When success equals true the file data can be deleted
		/// </summary>
		/// <param name="success">If set to <c>true</c> success.</param>
		void TransferComplete(bool success);
	}

	/// <summary>
	/// File ready handler. Will be called by the slave when a master sends a FILE READY (file download announcement) message to the slave.
	/// </summary>
	public delegate IFileReceiver FileReadyHandler (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile);

	/// <summary>
	/// Simple implementation of IFileProvider that can be used to provide transparent files. Derived classed should override the
	/// TransferComplete method.
	/// </summary>
	public class TransparentFile : IFileProvider
	{
		private List<byte[]> sections = new List<byte[]>();

		private DateTime time = DateTime.MinValue;

		private int ca;
		private int ioa;
		private NameOfFile nof;

		public TransparentFile(int ca, int ioa, NameOfFile nof)
		{
			this.ca = ca;
			this.ioa = ioa;
			this.nof = nof;
			time = DateTime.Now;
		}

		public void AddSection(byte[] section)
		{
			sections.Add(section);
		}

		public int GetCA ()
		{
			return ca;
		}

		public int GetIOA ()
		{
			return ioa;
		}

		public NameOfFile GetNameOfFile ()
		{
			return nof;
		}

		public DateTime GetFileDate ()
		{
			return time;
		}

		public int GetFileSize ()
		{
			int fileSize = 0;

			foreach (byte[] section in sections)
				fileSize += section.Length;

			return fileSize;
		}

		public int GetSectionSize (int sectionNumber)
		{
			if (sectionNumber < sections.Count)
				return sections [sectionNumber].Length;
			else
				return -1;
		}

		public bool GetSegmentData (int sectionNumber, int offset, int segmentSize, byte[] segmentData)
		{
			if ((sectionNumber >= sections.Count) || (sectionNumber < 0))
				return false;

			byte[] section = sections [sectionNumber];

			if (offset + segmentSize > section.Length)
				return false;

			for (int i = 0; i < segmentSize; i++)
				segmentData [i] = section [i + offset];

			return true;
		}

		public virtual void TransferComplete (bool success)
		{
		}
	}


	internal enum FileClientState {
		IDLE,
		WAITING_FOR_FILE_READY,
		WAITING_FOR_SECTION_READY, /* or for LAST_SECTION */
		RECEIVING_SECTION /* waiting for SEGMENT or LAST SEGMENT */
	}

	delegate void DebugLogger (string message);

	internal class FileClient
	{
		private FileClientState state = FileClientState.IDLE;
		private Master master;

		private int ca;
		private int ioa;
		private NameOfFile nof;
		private IFileReceiver fileReceiver = null;

		private DebugLogger DebugLog;

		private int segmentOffset = 0;

		public FileClient(Master master, DebugLogger debugLog)
		{
			this.master = master;
			DebugLog = debugLog;
		}

		private ASDU NewAsdu(InformationObject io) 
		{
			ASDU asdu = new ASDU (master.GetApplicationLayerParameters (), CauseOfTransmission.FILE_TRANSFER, false, false, 0, ca, false);

			asdu.AddInformationObject (io);

			return asdu;
		}

		private void ResetStateToIdle()
		{
			fileReceiver = null;
			state = FileClientState.IDLE;
		}

		private void AbortFileTransfer(FileErrorCode errorCode)
		{
			ASDU deactivateFile = NewAsdu (new FileCallOrSelect (ioa, nof, 0, SelectAndCallQualifier.DEACTIVATE_FILE));

			master.SendASDU (deactivateFile);

			if (fileReceiver != null)
				fileReceiver.Finished (errorCode);

			ResetStateToIdle ();
		}

		public bool HandleFileAsdu(ASDU asdu)
		{
			bool asduHandled = true;

			switch (asdu.TypeId) {

			case TypeID.F_SC_NA_1: /* File/Section/Directory Call/Select */

				DebugLog ("Received SELECT/CALL");

				if (state == FileClientState.WAITING_FOR_FILE_READY) {

					if (asdu.Cot == CauseOfTransmission.UNKNOWN_TYPE_ID) {
					
						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.UNKNOWN_SERVICE);
					} else if (asdu.Cot == CauseOfTransmission.UNKNOWN_COMMON_ADDRESS_OF_ASDU) {
					
						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.UNKNOWN_CA);
					} else if (asdu.Cot == CauseOfTransmission.UNKNOWN_INFORMATION_OBJECT_ADDRESS) {
					
						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.UNKNOWN_IOA);
					} else {
						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.PROTOCOL_ERROR);
					}
				} else {
					if (fileReceiver != null)
						fileReceiver.Finished (FileErrorCode.PROTOCOL_ERROR);
				}

				ResetStateToIdle ();

				break;

			case TypeID.F_FR_NA_1: /* File ready */

				DebugLog ("Received FILE READY");

				if (state == FileClientState.WAITING_FOR_FILE_READY) {

					//TODO check ca, ioa, nof

					FileReady fileReady = (FileReady)asdu.GetElement (0);

					if (fileReady.Positive) {

						ASDU callFile = NewAsdu (new FileCallOrSelect (ioa, nof, 0, SelectAndCallQualifier.REQUEST_FILE));
						master.SendASDU (callFile);

						DebugLog ("Send CALL FILE");

						state = FileClientState.WAITING_FOR_SECTION_READY;

					} else {
						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.FILE_NOT_READY);

						ResetStateToIdle ();
					}

				} else if (state == FileClientState.IDLE) {

					//TODO call user callback to 

					//TODO send positive or negative ACK

					state = FileClientState.WAITING_FOR_SECTION_READY;

				}
				else {
					AbortFileTransfer (FileErrorCode.PROTOCOL_ERROR);
				}

				break;

			case TypeID.F_SR_NA_1: /* Section ready */

				DebugLog ("Received SECTION READY");

				if (state == FileClientState.WAITING_FOR_SECTION_READY) {

					SectionReady sc = (SectionReady)asdu.GetElement (0);

					if (sc.NotReady == false) {

						ASDU callSection = NewAsdu(new FileCallOrSelect (ioa, nof, 0, SelectAndCallQualifier.REQUEST_SECTION));
						master.SendASDU (callSection);

						DebugLog ("Send CALL SECTION");

						segmentOffset = 0;
						state = FileClientState.RECEIVING_SECTION;

					} else {
						AbortFileTransfer(FileErrorCode.SECTION_NOT_READY);
					}

				} else if (state == FileClientState.IDLE) {
				} else {
					if (fileReceiver != null)
						fileReceiver.Finished (FileErrorCode.PROTOCOL_ERROR);

					ResetStateToIdle ();
				}

				break;

			case TypeID.F_SG_NA_1: /* Segment */

				DebugLog ("Received SEGMENT");

				if (state == FileClientState.RECEIVING_SECTION) {

					FileSegment segment = (FileSegment)asdu.GetElement (0);

					if (fileReceiver != null) {
						fileReceiver.SegmentReceived (segment.NameOfSection, segmentOffset, segment.LengthOfSegment, segment.SegmentData);
					}

					segmentOffset += segment.LengthOfSegment;
				} else if (state == FileClientState.IDLE) {
				} else {
					AbortFileTransfer (FileErrorCode.PROTOCOL_ERROR);
				}

				break;


			case TypeID.F_LS_NA_1: /* Last segment or section */

				DebugLog ("Received LAST SEGMENT/SECTION");

				if (state != FileClientState.IDLE) {

					FileLastSegmentOrSection lastSection = (FileLastSegmentOrSection)asdu.GetElement (0);

					if (lastSection.LSQ == LastSectionOrSegmentQualifier.SECTION_TRANSFER_WITHOUT_DEACT) {

						if (state == FileClientState.RECEIVING_SECTION) {

							ASDU segmentAck = NewAsdu (new FileACK (ioa, nof, lastSection.NameOfSection, AcknowledgeQualifier.POS_ACK_SECTION, FileError.DEFAULT));

							master.SendASDU (segmentAck);	

							DebugLog ("Send SEGMENT ACK");

							state = FileClientState.WAITING_FOR_SECTION_READY;
						} else {
							AbortFileTransfer (FileErrorCode.PROTOCOL_ERROR);
						}
					} else if (lastSection.LSQ == LastSectionOrSegmentQualifier.FILE_TRANSFER_WITH_DEACT) {
						/* slave aborted transfer */

						if (fileReceiver != null)
							fileReceiver.Finished (FileErrorCode.ABORTED_BY_REMOTE);

						ResetStateToIdle ();
					} else if (lastSection.LSQ == LastSectionOrSegmentQualifier.FILE_TRANSFER_WITHOUT_DEACT) {

						if (state == FileClientState.WAITING_FOR_SECTION_READY) {
							ASDU fileAck = NewAsdu (new FileACK (ioa, nof, lastSection.NameOfSection, AcknowledgeQualifier.POS_ACK_FILE, FileError.DEFAULT));

							master.SendASDU (fileAck);

							DebugLog ("Send FILE ACK");

							if (fileReceiver != null)
								fileReceiver.Finished (FileErrorCode.SUCCESS);

							ResetStateToIdle ();
						}
						else {

							DebugLog ("Illegal state: " + state.ToString ());

							AbortFileTransfer (FileErrorCode.PROTOCOL_ERROR);
						}
					}
				} 

				break;

			default:

				asduHandled = false;
				break;
			}


			return asduHandled;
		}

		public void HandleFileService()
		{
			//TODO timeout handling
		}

		public void RequestFile(int ca, int ioa, NameOfFile nof, IFileReceiver fileReceiver)
		{
			this.ca = ca;
			this.ioa = ioa;
			this.nof = nof;
			this.fileReceiver = fileReceiver;

			ASDU selectFile = NewAsdu(new FileCallOrSelect (ioa, nof, 0, SelectAndCallQualifier.SELECT_FILE));

			master.SendASDU (selectFile);

			state = FileClientState.WAITING_FOR_FILE_READY;
		}

	}




	internal enum FileServerState {
		UNSELECTED_IDLE,
		WAITING_FOR_FILE_CALL,
		WAITING_FOR_SECTION_CALL,
		TRANSMIT_SECTION,
		WAITING_FOR_SECTION_ACK,
		WAITING_FOR_FILE_ACK,
		SEND_ABORT,
		TRANSFER_COMPLETED
	}

	/// <summary>
	/// Encapsulates a IFileProvider object to add some state information
	/// </summary>
	internal class CS101n104File {

		public CS101n104File(IFileProvider file)
		{
			this.provider = file;
		}

		public IFileProvider provider = null;
		public object selectedBy = null;

	}


	public class FilesAvailable
	{

		private List<CS101n104File> availableFiles = new List<CS101n104File>();

		internal CS101n104File GetFile(int ca, int ioa, NameOfFile nof)
		{
			lock (availableFiles) {

				foreach (CS101n104File file in availableFiles) {
					if ((file.provider.GetCA () == ca) && (file.provider.GetIOA () == ioa)) {

						if (nof == NameOfFile.DEFAULT)
							return file;
						else {

							if (nof == file.provider.GetNameOfFile ())
								return file;
						}
					}
				}
			}

			return null;
		}

		internal void SendDirectoy(IMasterConnection masterConnection, bool spontaneous)
		{
			CauseOfTransmission cot;

			if (spontaneous)
				cot = CauseOfTransmission.SPONTANEOUS;
			else
				cot = CauseOfTransmission.REQUEST;

			lock (availableFiles) {

				int size = availableFiles.Count;
				int i = 0;

				int currentCa = -1;
				int currentIOA = -1;

				ASDU directoryAsdu = null; 

				foreach (CS101n104File file in availableFiles) {
				
					bool newAsdu = false;

					if (file.provider.GetCA () != currentCa) {
						currentCa = file.provider.GetCA ();
						newAsdu = true;
					}

					if (currentIOA != (file.provider.GetIOA () - 1)) {
						newAsdu = true;
					}

					if (newAsdu) {
						if (directoryAsdu != null) {
							masterConnection.SendASDU (directoryAsdu);
							directoryAsdu = null;
						}
					}

					currentIOA = file.provider.GetIOA ();

					i++;

					if (directoryAsdu == null) {
						Console.WriteLine ("Send directory ASDU");
						directoryAsdu = new ASDU (masterConnection.GetApplicationLayerParameters (), cot, false, false, 0, currentCa, true);
					}

					bool lastFile = (i == size);

					byte sof = 0;

					if (lastFile)
						sof = 0x20;

					InformationObject io = new FileDirectory(currentIOA, file.provider.GetNameOfFile(), file.provider.GetFileSize(), sof, new CP56Time2a(file.provider.GetFileDate()));

					directoryAsdu.AddInformationObject (io);
				}

				if (directoryAsdu != null) {

					Console.WriteLine ("Send directory ASDU");
					masterConnection.SendASDU (directoryAsdu);
				}

			}
		}

		public void AddFile(IFileProvider file)
		{
			lock (availableFiles) {

				availableFiles.Add (new CS101n104File (file));
			}
				
		}

		public void RemoveFile(IFileProvider file)
		{
			lock (availableFiles) {

				foreach (CS101n104File availableFile in availableFiles) {

					if (availableFile.provider == file) {
						availableFiles.Remove (availableFile);
						return;
					}

				}
			}
		}

	}



	internal class FileServer {

		public FileServer(IMasterConnection masterConnection, FilesAvailable availableFiles, DebugLogger logger) 
		{
			transferState = FileServerState.UNSELECTED_IDLE;
			alParameters = masterConnection.GetApplicationLayerParameters();
			maxSegmentSize = FileSegment.GetMaxDataSize (alParameters);
			this.availableFiles = availableFiles;
			this.logger = logger;
			this.connection = masterConnection;
		}

		private FilesAvailable availableFiles;

		private CS101n104File selectedFile;

		private DebugLogger logger;

		private ApplicationLayerParameters alParameters;

		private IMasterConnection connection;
		private int maxSegmentSize;

		private byte currentSectionNumber;
		private int currentSectionSize;
		private int currentSectionOffset;
		private byte sectionChecksum = 0;
		private byte fileChecksum = 0;

		private FileServerState transferState;

		private void SendDirectory()
		{
			
		}

		public bool HandleFileAsdu(ASDU asdu)
		{
			bool handled = true;

			switch (asdu.TypeId) {

			case TypeID.F_AF_NA_1: /*  124 - ACK file, ACK section */

				logger ("Received file/section ACK F_AF_NA_1");

				if (asdu.Cot == CauseOfTransmission.FILE_TRANSFER) {

					if (transferState != FileServerState.UNSELECTED_IDLE) {

						IFileProvider file = selectedFile.provider;

						FileACK ack = (FileACK)asdu.GetElement (0);

						if (ack.AckQualifier == AcknowledgeQualifier.POS_ACK_FILE) {

							logger ("Received positive file ACK");

							if (transferState == FileServerState.WAITING_FOR_FILE_ACK) {

								selectedFile.provider.TransferComplete (true);

								availableFiles.RemoveFile (selectedFile.provider);

								selectedFile = null;

								transferState = FileServerState.UNSELECTED_IDLE;
							} else {
								logger ("Unexpected file transfer state --> abort file transfer");

								transferState = FileServerState.SEND_ABORT;
							}


						} else if (ack.AckQualifier == AcknowledgeQualifier.NEG_ACK_FILE) {

							logger ("Received negative file ACK - stop transfer");

							if (transferState == FileServerState.WAITING_FOR_FILE_ACK) {

								selectedFile.provider.TransferComplete (false);

								selectedFile.selectedBy = null;
								selectedFile = null;

								transferState = FileServerState.UNSELECTED_IDLE;
							} else {
								logger ("Unexpected file transfer state --> abort file transfer");

								transferState = FileServerState.SEND_ABORT;
							}

						} else if (ack.AckQualifier == AcknowledgeQualifier.NEG_ACK_SECTION) {

							logger ("Received negative file section ACK - repeat section");

							if (transferState == FileServerState.WAITING_FOR_SECTION_ACK) {
								currentSectionOffset = 0;
								sectionChecksum = 0;

								ASDU sectionReady = new ASDU (alParameters, CauseOfTransmission.FILE_TRANSFER, false, false, 0, file.GetCA (), false);

								sectionReady.AddInformationObject (
									new SectionReady (selectedFile.provider.GetIOA (), selectedFile.provider.GetNameOfFile (), currentSectionNumber, currentSectionSize, false));

								connection.SendASDU (sectionReady);


								transferState = FileServerState.TRANSMIT_SECTION;
							}
							else {
								logger ("Unexpected file transfer state --> abort file transfer");

								transferState = FileServerState.SEND_ABORT;
							}

						} else if (ack.AckQualifier == AcknowledgeQualifier.POS_ACK_SECTION) {

							if (transferState == FileServerState.WAITING_FOR_SECTION_ACK) {

								currentSectionNumber++;

								int nextSectionSize = 
									selectedFile.provider.GetSectionSize (currentSectionNumber);

								ASDU responseAsdu = new ASDU (alParameters, CauseOfTransmission.FILE_TRANSFER, false, false, 0, file.GetCA (), false);

								if (nextSectionSize == -1) {
									logger ("Reveived positive file section ACK - send last section indication");

									responseAsdu.AddInformationObject (
										new FileLastSegmentOrSection (file.GetIOA (), file.GetNameOfFile (), 
											(byte)currentSectionNumber, 
											LastSectionOrSegmentQualifier.FILE_TRANSFER_WITHOUT_DEACT,
											fileChecksum));

									transferState = FileServerState.WAITING_FOR_FILE_ACK;
								} else {
									logger ("Reveived positive file section ACK - send next section ready indication");

									currentSectionSize = nextSectionSize;

									responseAsdu.AddInformationObject (
										new SectionReady (selectedFile.provider.GetIOA (), selectedFile.provider.GetNameOfFile (), currentSectionNumber, currentSectionSize, false));

									transferState = FileServerState.WAITING_FOR_SECTION_CALL;
								}

								connection.SendASDU (responseAsdu);

								sectionChecksum = 0;
							}
							else {
								logger ("Unexpected file transfer state --> abort file transfer");

								transferState = FileServerState.SEND_ABORT;
							}
						}
					} else {
						// No file transmission in progress --> what to do?
						logger ("Unexpected File ACK message -> ignore");
					}

				} else {
					asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
					connection.SendASDU (asdu);
				}
				break;

			case TypeID.F_SC_NA_1: /* 122 - Call/Select directoy/file/section */

				logger ("Received call/select F_SC_NA_1");

				if (asdu.Cot == CauseOfTransmission.FILE_TRANSFER) {

					FileCallOrSelect sc = (FileCallOrSelect)asdu.GetElement (0);


					if (sc.SCQ == SelectAndCallQualifier.SELECT_FILE) {

						if (transferState == FileServerState.UNSELECTED_IDLE) {

							logger ("Received SELECT FILE");

							CS101n104File file = availableFiles.GetFile (asdu.Ca, sc.ObjectAddress, sc.NOF);

							if (file == null) {
								asdu.Cot = CauseOfTransmission.UNKNOWN_INFORMATION_OBJECT_ADDRESS;
								connection.SendASDU (asdu);
							} else {

								ASDU fileReady = new ASDU (alParameters, CauseOfTransmission.FILE_TRANSFER, false, false, 0, asdu.Ca, false);

								// check if already selected
								if (file.selectedBy == null) {

									file.selectedBy = this;

									fileReady.AddInformationObject (new FileReady (sc.ObjectAddress, sc.NOF, file.provider.GetFileSize (), true));

								} else {
									fileReady.AddInformationObject (new FileReady (sc.ObjectAddress, sc.NOF, 0, false));
								}

								connection.SendASDU (fileReady);

								selectedFile = file;

								transferState = FileServerState.WAITING_FOR_FILE_CALL;
							}

						} else {
							logger ("Unexpected SELECT FILE message");
						}

					} else if (sc.SCQ == SelectAndCallQualifier.DEACTIVATE_FILE) {

						logger ("Received DEACTIVATE FILE");

						if (transferState != FileServerState.UNSELECTED_IDLE) {

							if (selectedFile != null) {
								selectedFile.selectedBy = null;
								selectedFile = null;
							}

							transferState = FileServerState.UNSELECTED_IDLE;


						} else {
							logger ("Unexpected DEACTIVATE FILE message");
						}


					}

					else if (sc.SCQ == SelectAndCallQualifier.REQUEST_FILE) {

						logger ("Received CALL FILE");

						if (transferState == FileServerState.WAITING_FOR_FILE_CALL) {

							if (selectedFile.provider.GetIOA () != sc.ObjectAddress) {
								logger ("Unkown IOA");

								asdu.Cot = CauseOfTransmission.UNKNOWN_INFORMATION_OBJECT_ADDRESS;
								connection.SendASDU (asdu);
							} else {

								ASDU sectionReady = new ASDU (alParameters, CauseOfTransmission.FILE_TRANSFER, false, false, 0, asdu.Ca, false);

								sectionReady.AddInformationObject (new SectionReady (sc.ObjectAddress, selectedFile.provider.GetNameOfFile (), 0, 0, false));

								connection.SendASDU (sectionReady);

								logger ("Send SECTION READY");

								currentSectionNumber = 0;
								currentSectionOffset = 0;
								currentSectionSize = selectedFile.provider.GetSectionSize (0);

								transferState = FileServerState.WAITING_FOR_SECTION_CALL;
							}

						} else {
							logger ("Unexpected FILE CALL message");
						}


					} else if (sc.SCQ == SelectAndCallQualifier.REQUEST_SECTION) {

						logger ("Received CALL SECTION");

						if (transferState == FileServerState.WAITING_FOR_SECTION_CALL) {

							if (selectedFile.provider.GetIOA () != sc.ObjectAddress) {
								logger ("Unkown IOA");

								asdu.Cot = CauseOfTransmission.UNKNOWN_INFORMATION_OBJECT_ADDRESS;
								connection.SendASDU (asdu);
							} else {

								transferState = FileServerState.TRANSMIT_SECTION;
							}
						} else {
							logger ("Unexpected SECTION CALL message");
						}
					}

				} else if (asdu.Cot == CauseOfTransmission.REQUEST) {
					logger ("Call directory received");

					availableFiles.SendDirectoy (connection, false);

				} else {
					asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
					connection.SendASDU (asdu);
				}
				break;

			default:
				handled = false;
				break;
			}


			return handled;
		}

		public void HandleFileTransmission() 
		{


			if (transferState != FileServerState.UNSELECTED_IDLE) {

				if (transferState == FileServerState.TRANSMIT_SECTION) {

					if (selectedFile != null) {

						IFileProvider file = selectedFile.provider;

						ASDU fileAsdu = new ASDU (alParameters, CauseOfTransmission.FILE_TRANSFER, false, false, 0, file.GetCA (), false);


						if (currentSectionOffset == currentSectionSize) {

							// send last segment

							fileAsdu.AddInformationObject (
								new FileLastSegmentOrSection (file.GetIOA(), file.GetNameOfFile (), 
									currentSectionNumber, 
									LastSectionOrSegmentQualifier.SECTION_TRANSFER_WITHOUT_DEACT,
									sectionChecksum));

							fileChecksum += sectionChecksum;
							sectionChecksum = 0;


							logger ("Send LAST SEGMENT");

							connection.SendASDU (fileAsdu);

							transferState = FileServerState.WAITING_FOR_SECTION_ACK;

						} else {

							int currentSegmentSize = currentSectionSize - currentSectionOffset;

							if (currentSegmentSize > maxSegmentSize)
								currentSegmentSize = maxSegmentSize;

							byte[] segmentData = new byte[currentSegmentSize];

							file.GetSegmentData (currentSectionNumber,
								currentSectionOffset,
								currentSegmentSize,
								segmentData);

							fileAsdu.AddInformationObject (
								new FileSegment (file.GetIOA(), file.GetNameOfFile (), currentSectionNumber, 
									segmentData));

							byte checksum = 0;

							foreach (byte octet in segmentData) {
								checksum += octet;
							}



							connection.SendASDU (fileAsdu);

							sectionChecksum += checksum;

							logger ("Send SEGMENT (CHS=" + sectionChecksum + ")");
							currentSectionOffset += currentSegmentSize;

						}
					}
				}

			}


		}


	}





}