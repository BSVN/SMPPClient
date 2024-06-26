﻿#region Namespaces

using System;
using System.Collections.Generic;

#endregion

namespace BSN.SmppClient
{
	/// <summary> IPacket Interface </summary>
	public interface IPacket
	{
        /// <summary> Interface to support processing PDU's </summary>
		byte[] GetPDU();
	}
}
