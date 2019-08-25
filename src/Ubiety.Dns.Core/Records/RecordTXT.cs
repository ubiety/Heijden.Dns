/*
 * Licensed under the MIT license
 * See the LICENSE file in the project root for more information
 */

/*
3.3.14. TXT RDATA format

    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
    /                   TXT-DATA                    /
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

where:

TXT-DATA        One or more <character-string>s.

TXT RRs are used to hold descriptive text.  The semantics of the text
depends on the domain where it is found.
 *
*/

using System.Collections.Generic;
using System.Text;

namespace Ubiety.Dns.Core.Records
{
    /// <summary>
    ///     Text DNS record.
    /// </summary>
    public class RecordTxt : Record
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RecordTxt" /> class.
        /// </summary>
        /// <param name="rr"><see cref="RecordReader" /> for the record data.</param>
        /// <param name="length">Record length.</param>
        public RecordTxt(RecordReader rr, int length)
        {
            var position = rr.Position;
            Text = new List<string>();
            while ((rr.Position - position) < length)
            {
                Text.Add(rr.ReadString());
            }
        }

        /// <summary>
        ///     Gets the text.
        /// </summary>
        public List<string> Text { get; }

        /// <summary>
        ///     String representation of the record data.
        /// </summary>
        /// <returns>Text as a string.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var item in Text)
            {
                sb.Append(item);
            }

            return sb.ToString().TrimEnd();
        }
    }
}