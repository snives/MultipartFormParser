using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultipartFormParser
{
    public static class BinaryReaderExtension
    {
        public static string ReadLine(this BinaryReader reader)
        {
            if (reader.EndOfStream())
                return null;

            StringBuilder result = new StringBuilder();
            char character;
            while (!reader.EndOfStream() && (character = reader.ReadChar()) != '\n')
                if (character != '\r' && character != '\n')
                    result.Append(character);

            return result.ToString();
        }

        public static bool EndOfStream(this BinaryReader reader)
        {
            return reader.BaseStream.Position == reader.BaseStream.Length;
        }

        public static byte[] ReadUntil(this BinaryReader reader, byte[] delimiter)
        {
            int matchIx = 0;
            int matchTarget=delimiter.Length;

            var ms = new MemoryStream();

            while (matchIx < matchTarget && !reader.EndOfStream())
            {
                var b = reader.ReadByte();
                ms.WriteByte(b);

                if (b == delimiter[matchIx])
                    matchIx++;
                else
                {
                    matchIx = 0;
                    if (b == delimiter[matchIx])
                        matchIx++;
                }
            }
            
            //truncate the delimiter
            ms.SetLength(ms.Length - matchTarget);

            //rewind
            if (reader.BaseStream.CanSeek)
                reader.BaseStream.Seek(-matchTarget, SeekOrigin.Current);

            return ms.ToArray();
        }
    }
}
