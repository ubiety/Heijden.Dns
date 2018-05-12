using System;
/*

 */

namespace Ubiety.Dns.Core.Records.NotUsed
{
        /// <summary>
        /// </summary>
    public class RecordOPT : Record
    {
        /// <summary>
        /// </summary>
        public byte[] RDATA;

        /// <summary>
        /// </summary>
        public RecordOPT(RecordReader rr)
        {
            // re-read length
            ushort RDLENGTH = rr.ReadUInt16(-2);
            RDATA = rr.ReadBytes(RDLENGTH);
        }

        /// <summary>
        /// </summary>
        public override string ToString()
        {
            return string.Format("not-used");
        }

    }
}
