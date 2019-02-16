using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;

namespace DatastoreMiner
{
    /// <summary>
    /// Catalogue based on a CSV file
    /// </summary>
    class CSVCatalogue : ICatalogueReader
    {
        protected string _LineEndings = "\r\a"; //line ending for the catalogue file - might need to override this
        public string LineEndings
        {
            get { return _LineEndings; }
            set { _LineEndings = value; }
        }

        public DataTable Catalogue;
        

        /// <summary>
        /// Constructor to load a catalogue from a file with column name mappings
        /// </summary>
        public CSVCatalogue()
        {
        }
        
        /// <summary>
        /// Read a catalogue from a CSV file, applying the specified transformation on the columns.
        /// Method to load the catalogue from a data file (manifest) into this.Catalogue
        /// </summary>
        /// <param name="CatalogueFile">The file to load the catalogue from</param>
        public DataTable ReadCatalogue(string CatalogueFile)
        {
            this.Catalogue = new DataTable("Datastore");

            //initialise count data for dataset
            //_DatasetCount = 0; //total number of datasets
            //_CSVLinkCount = 0; //number of datasets containing csv links
            //_ZIPLinkCount = 0; //number of zip of gzip links

            //TextReader reader = File.OpenText(CatalogueFile);
            StreamReader reader = new StreamReader(CatalogueFile, Encoding.ASCII, true);
            try
            {
                string Line, NextLine;
                //read the header line
                Line = ReadLineWithEndings(reader);
                string[] Headers = ParseCSVLine(Line);
                foreach (string Header in Headers)
                {
                    try
                    {
                        Catalogue.Columns.Add(new DataColumn(Header, typeof(string)));
                    }
                    catch (DuplicateNameException)
                    {
                        int i = 2;
                        while (Catalogue.Columns.IndexOf(Header + "-" + i) >= 0) i++;
                        Catalogue.Columns.Add(new DataColumn(Header + "-" + i, typeof(string)));
                        System.Diagnostics.Debug.WriteLine("Column " + Header + " already exists: renamed to " + Header + "-" + i);
                    }
                }

                //int LinkColIdx = Catalogue.Columns.IndexOf(LinkField);
                Line = ReadLineWithEndings(reader); //initialise continuation line reader
                do
                {
                    while (((NextLine = ReadLineWithEndings(reader)) != null) && (NextLine == "\\"))
                    {
                        //read continuation lines
                        //format is text followed by "\" on a single line followed by text (and repeated)
                        Line += ReadLineWithEndings(reader);
                    }
                    //now we have all the continuation lines, process the finished csv line
                    string[] Values = ParseCSVLine(Line);
                    //System.Diagnostics.Debug.WriteLine("Description: " + Values[22]); //hack for GovDatastore description column
                    if (Catalogue.Columns.Count < Values.Length)
                    {
                        System.Diagnostics.Debug.WriteLine("Error: Too many columns in: " + Line);
                    }
                    else
                    {
                        DataRow Row = Catalogue.Rows.Add(Values);
                        //++_DatasetCount;
                        //string Link = Row[LinkColIdx] as string;
                        //if (!string.IsNullOrEmpty(Link))
                        //{
                        //    Link = Link.ToLower();
                        //    if (Link.EndsWith(".csv"))
                        //        ++_CSVLinkCount;
                        //    else if (Link.EndsWith(".zip"))
                        //        ++_ZIPLinkCount;
                        //    else if (Link.EndsWith(".gz"))
                        //        ++_ZIPLinkCount;
                        //}
                    }

                    //finished processing this line, so start a new one
                    Line = NextLine;
                } while (Line != null);
            }
            finally
            {
                reader.Close();
            }
            return this.Catalogue;
        }

        /// <summary>
        /// Read everything up to the next CR character. Necessary because LF (0x0A) characters are embedded inside fields in the Gov Datastore catalogue file and
        /// the standard StreamReader.ReadLine breaks the line at the next LF or CRLF.
        /// Also prevents line breaks that occur between quotes.
        /// TODO: make sure this is buffered otherwise the performance is going to be horrible!
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private string ReadCRLine(StreamReader reader)
        {
            StringBuilder builder = new StringBuilder();
            char[] ch = new char[1];
            int count = 0;
            bool InQuote = false;
            do
            {
                //char ch = (char)reader.BaseStream.ReadByte(); //NO, you miss the buffered characters
                count = reader.Read(ch, 0, 1);
                if (count == 1)
                {
                    if (ch[0] == '"') InQuote = !InQuote; //prevent end of line breaks that occur within quotes
                    else if ((!InQuote) && (ch[0] == '\r')) break; //CR character 0x0D=13 or \r
                    builder.Append(ch[0]);
                }
            } while (count == 1);

            string Line = builder.ToString().Trim(); //strip off leading \n (0x0a) character from previous \r\n line ending
            if (count == 0) return null; //return null to signal end of data stream

            return Line;
        }

        /// <summary>
        /// Read a line from a file with either a CR ending or LF.
        /// Basically, this is a bit of a hack, but it enables the code to load the catalogue for the London Datastore or data.gov.uk which
        /// have different line endings and contain newlines within quotes inside complex CSV continuation lines.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private string ReadLineWithEndings(StreamReader reader)
        {
            if (this._LineEndings == "\r") return ReadCRLine(reader);
            else if (this._LineEndings == "\r\a") return reader.ReadLine();
            return null;
        }

        /// <summary>
        /// Break a  csv line up into separate fields
        /// Made this public so I can re-use it. Not really the best way of doing this.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static string[] ParseCSVLine(string line)
        {
            List<string> Items = new List<string>();
            string Current = "";
            bool Quote = false;
            foreach (char ch in line)
            {
                switch (ch)
                {
                    case ',':
                        if (!Quote)
                        {
                            Items.Add(Current.Trim());
                            Current = "";
                        }
                        else Current += ","; //comma inside a quote
                        break;
                    case '"':
                        Quote = !Quote;
                        break;
                    default:
                        Current += ch;
                        break;
                }
            }
            Items.Add(Current.Trim()); //add trailing item - even if last char was a comma, we still want a null on the end

            return Items.ToArray();
        }
    }
}
