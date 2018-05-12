using System;
/*

 */

namespace Ubiety.Dns.Core.Records.NotUsed
{
        /// <summary>
        /// </summary>
    public class RecordUID : Record
    {
        /// <summary>
        /// </summary>
        public byte[] RDATA;

        /// <summary>
        /// </summary>
        public RecordUID(RecordReader rr)
        {
            // re-read length
            ushort RDLENGTH = rr.ReadUInt16(-2);
            RDATA = rr.ReadBytes(RDLENGTH);
        }

        /// <summary>
        /// </summary>
        public override string ToString()
        {
            return "not-used";
        }

    }
}
