using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/*
 *  MultipartFormParser - Reads and writes multipart form post data and provides
 *  a simple dictionary interface for interacting with and modifying the form post.
 *  
 *  Copyright (C) 2016 Snives
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details. 
 * 
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */


namespace MultipartFormParser
{
    public class MultipartFormParser
    {
        const string ContentTransferEncoding = "Content-Transfer-Encoding: ";
        const string ContentDisposition = "Content-Disposition: form-data;";
        const string ContentType = "Content-Type: ";


        public string Boundary { get; set; }

        public Dictionary<string, Field> Parse(Stream stream, Encoding encoding)
        {
            Regex nameRegex = new Regex(@"(?<=name)=""(?<name>.*?)""");
            Regex filenameRegex = new Regex(@"(?<=filename)=""(?<filename>.*?)""");

            var fields = new Dictionary<string, Field>();

            //Though we have defined the encoding here, its possible to have binary data in uploaded file fields.
            var reader = new BinaryReader(stream, encoding);
            
            // The first line should contain the boundary (delimiter) which separates each field of the form.
            Boundary = reader.ReadLine();
            
            //If it doesn't then it could be application/x-url-encoded method which is just a urlencoded string

            //Loop through boundaries, parsing each field into a field.
            while (!reader.EndOfStream())
            {
                var field = new Field();
                var line = reader.ReadLine();
                while (line != null && line != Boundary)
                {
                    //Read the components of this field.
                    if (line.StartsWith(ContentDisposition))
                    {
                        //Capture Name
                        var matchName = nameRegex.Match(line, ContentDisposition.Length);
                        if (matchName.Success)
                        {
                            field.Name = matchName.Groups["name"].Value;
                        }

                        //Capture Filename
                        var matchFilename = filenameRegex.Match(line, 32);
                        if (matchFilename.Success)
                        {
                            field.Filename = matchFilename.Groups["filename"].Value;
                        }
                    }
                    
                    if (line.StartsWith(ContentType))
                    {
                        field.ContentType = line.Substring(ContentType.Length);
                    }

                    if (line.StartsWith(ContentTransferEncoding))
                    {
                        field.ContentTransferEncoding = line.Substring(ContentTransferEncoding.Length);
                    }

                    if (line == string.Empty)
                    {
                        //This indicates field headers have been sent, the next line will be data until we reach a boundary
                        
                        //Switch by encoding type
                        //See Section 5 regarding Content-Transfer-Encoding https://www.ietf.org/rfc/rfc2046.txt
                        //https://www.w3.org/Protocols/rfc1341/5_Content-Transfer-Encoding.html
                        switch (field.ContentTransferEncoding)
                        {
                            case null:
                            case "7bit":
                                break;
                            case "8bit":
                                break;
                            case "quoted-printable":
                                break;
                            case "base64":
                                break;
                            case "binary":
                                //implement a read until you reach the boundary method
                                break;
                        }

                        //It is apparent that handling data is not specific to the Content-Transfer-Encoding, so treat it always as binary.

                        if (!string.IsNullOrEmpty(field.Filename))
                            field.Bytes = reader.ReadUntil(encoding.GetBytes(Environment.NewLine + Boundary));
                        else
                            field.Value = encoding.GetString(reader.ReadUntil(encoding.GetBytes(Environment.NewLine + Boundary)));
                        
                        //consume the dangling newline
                        reader.ReadChars(2);
                    }

                    line = reader.ReadLine();
                }

                //Add the field
                fields.Add(field.Name, field);
            }

            return fields;

        }

        public byte[] GetMultipartFormData(Dictionary<string, Field> fieldCollection)
        {
            var encoding = Encoding.UTF8;

            var ms = new MemoryStream();

            foreach (var kvp in fieldCollection)
            {
                //Write boundary
                ms.Write(encoding.GetBytes(Boundary), 0, encoding.GetByteCount(Boundary));
                ms.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                //Write Content-Disposition, name, and optionally filename
                var cd = string.Format(@"{0} name=""{1}""", ContentDisposition, kvp.Value.Name);
                if (!string.IsNullOrEmpty(kvp.Value.Filename))
                    cd += string.Format(@"; filename=""{0}""", kvp.Value.Filename);
                cd += "\r\n";

                ms.Write(encoding.GetBytes(cd), 0, encoding.GetByteCount(cd));

                //Write Content-Type
                if (!string.IsNullOrEmpty(kvp.Value.ContentType))
                {
                    var ct = string.Format("{0}{1}\r\n", ContentType, kvp.Value.ContentType);
                    ms.Write(encoding.GetBytes(ct), 0, encoding.GetByteCount(ct));
                }

                //Write Content-Transfer-Encoding
                if (!string.IsNullOrEmpty(kvp.Value.ContentTransferEncoding))
                {
                    var cte = string.Format("{0}{1}\r\n", ContentTransferEncoding, kvp.Value.ContentTransferEncoding);
                    ms.Write(encoding.GetBytes(cte), 0, encoding.GetByteCount(cte));
                }

                //Write newline to indicate value/data will follow
                ms.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                //Write value or data if present
                if (!string.IsNullOrEmpty(kvp.Value.Value))
                {
                    ms.Write(encoding.GetBytes(kvp.Value.Value), 0, encoding.GetByteCount(kvp.Value.Value));
                } 
                else if (kvp.Value.Bytes != null && kvp.Value.Bytes.Length > 0)
                {
                    ms.Write(kvp.Value.Bytes, 0, kvp.Value.Bytes.Length);
                }

                ms.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));
            }

            //Finish with one more boundary, final boundary must end with "--"
            ////https://www.w3.org/Protocols/rfc1341/7_2_Multipart.html
            ms.Write(encoding.GetBytes(Boundary + "--"), 0, encoding.GetByteCount(Boundary + "--"));
            ms.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

            return ms.ToArray();
        }

        public class Field
        {
            public string Name { get; set; }
            public string ContentType { get; set; }
            public string ContentInfo { get; set; }
            public string ContentDescription { get; set; }
            public string ContentTransferEncoding { get; set; }
            public string Filename { get; set; }
            public string Value { get; set; }
            public byte[] Bytes { get; set; }
        }

    }
}
