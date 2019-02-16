using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Text;

//Stolen from MapTube
//modified to add a loadheaders method

namespace MapTube.GIS
{
    /// <summary>
    /// DataLoader is a class for handling the loading of map data from CSV files from the web.
    /// </summary>
    public class DataLoader
    {
        private static int MAX_DISCRETE_VALUES = 12; //was 25!
        private bool IsDiscreteValues = true;
        private Hashtable hash = new Hashtable();
        //TODO: need num groups and frequency histogram (second pass?)

        //Status code returned when data is loaded, _WITH_WARNINGS suggests you should look at the Warnings text
        public enum StatusCode { SUCCESS, SUCCESS_WITH_WARNINGS, FAIL };

        private List<float> dataset;
        public List<float> DataSet
        {
            get { return dataset; }
        }

        private float mindatavalue, maxdatavalue;
        public float MinDataValue
        {
            get { return mindatavalue; }
        }
        public float MaxDataValue
        {
            get { return maxdatavalue; }
        }

        private string LastError = "";
        public string LastErrorText
        {
            get { return LastError; }
        }

        private StringBuilder Warnings = new StringBuilder(); //detailed report on data loading issues
        public string WarningsText
        {
            get { return Warnings.ToString(); }
        }

        /// <summary>
        /// Return true if the list contains discrete data items i.e. less than MAX_DISCRETE_VALUES (=25)
        /// </summary>
        public bool IsDiscreteData
        {
            get { return IsDiscreteValues; }
        }

        /// <summary>
        /// Return a list of the discrete values found in ascending order
        /// </summary>
        public List<float> GetDiscreteValues
        {
            get
            {
                if (!this.IsDiscreteValues) return new List<float>();
                List<float> discrete = new List<float>();
                foreach (Object o in hash.Keys)
                {
                    discrete.Add(Convert.ToSingle(o));
                }
                discrete.Sort();
                return discrete;
            }
        }

        public DataLoader()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        /// <summary>
        /// Splits a string containing comma separated data and returns the elements as an array of strings.
        /// Quotes can be used around items containing a comma.
        /// These quotes are removed from the string array that is returned.
        /// </summary>
        /// <param name="line">The CSV line string to be split</param>
        /// <returns>Array of strings containing the comma separated elements in the CSV file line</returns>
        public string[] ParseCSVLine(string line)
        {
            try
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
            catch (Exception ex)
            {
                LastError = "Error parsing: " + line + "\n" + ex.Message;
                Warnings.AppendLine("FATAL ERROR: " + LastError);
                Warnings.AppendLine("Processing aborted.");
            }
            return null;
        }

        /// <summary>
        /// Load just the header line
        /// </summary>
        /// <param name="URIDataFilename"></param>
        /// <param name="Headers"></param>
        public void LoadHeaderLine(string URIDataFilename, out string[] Headers)
        {
            string line = "[start of file]";
            using (System.Net.WebClient webClient = new System.Net.WebClient())
            {
                RequestCachePolicy policy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                webClient.CachePolicy = policy;
                try
                {
                    using (System.IO.Stream stream = webClient.OpenRead(URIDataFilename))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            line = reader.ReadLine(); //read the header line
                            Headers = ParseCSVLine(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Headers = null;
                }
            }
        }

        /// <summary>
        /// Load CSV data from a URI (e.g. DataDrop).
        /// </summary>
        /// <param name="URIDataFilename">The location of the data</param>
        /// <param name="DataField">The name of the field containing the data column</param>
        /// <param name="Headers">The column header row to be returned</param>
        /// <returns>A status code which indicates whether the file loaded perfectly, with warnings, or failed to load. If it
        /// loaded with warnings, then the Warnings property contains the text.</returns>
        public StatusCode LoadCSVData(string URIDataFilename, string DataField, out string[] Headers)
        {
            //TODO: set an upper bound on the number of points that can be loaded to prevent really large files?
            LastError = "";
            Warnings = new StringBuilder();

            Headers = null;

            mindatavalue = float.MaxValue;
            maxdatavalue = float.MinValue;
            this.dataset = new List<float>();
            IsDiscreteValues = true;
            this.hash.Clear();

            string line = "[start of file]";
            int linecount = 0;
            using (System.Net.WebClient webClient = new System.Net.WebClient())
            {
                RequestCachePolicy policy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                webClient.CachePolicy = policy;
                try
                {
                    using (System.IO.Stream stream = webClient.OpenRead(URIDataFilename))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            int DataPos = -1;
                            line = reader.ReadLine(); //read the header line
                            ++linecount;
                            Headers = ParseCSVLine(line);
                            if (!string.IsNullOrEmpty(DataField))
                            {
                                for (int i = 0; i < Headers.Length; i++)
                                {
                                    if (Headers[i] == DataField) DataPos = i;
                                }
                            }
                            //if datapos ==-1, then exit as something went wrong, but not if the field string is null (points
                            //don't have to have a data field) or if we're loading all the data (LoadAllFields==true)
                            if ((!string.IsNullOrEmpty(DataField)) && (DataPos == -1))
                            {
                                LastError = string.Format("Data field '{0}' not found on header line of csv file", DataField);
                                Warnings.AppendLine("FATAL ERROR: " + LastError);
                                return StatusCode.FAIL;
                            }

                            //now read the data lines
                            while (!reader.EndOfStream)
                            {
                                line = reader.ReadLine();
                                ++linecount;
                                if ((line != null) && (line.Length > 0))
                                {
                                    string[] fields = ParseCSVLine(line);
                                    if (fields.Length >= Headers.Length)
                                    {
                                        string data = "";
                                        float value = float.NaN;
                                        if (DataPos > -1)
                                        {
                                            data = fields[DataPos].Trim();
                                            try
                                            {
                                                value = Convert.ToSingle(fields[DataPos]);
                                            }
                                            catch
                                            {
                                                Warnings.AppendLine("WARNING: Field \""+ data +"\" could not be interpreted as a number on line "+linecount+" \"" + line + "\" - skipping line");
                                                continue;
                                            }
                                            dataset.Add(value);
                                        }

                                        //and update the metadata
                                        if (value <= mindatavalue) mindatavalue = value;
                                        if (value >= maxdatavalue) maxdatavalue = value;
                                        if (IsDiscreteValues)
                                        {
                                            if (hash.Count < MAX_DISCRETE_VALUES)
                                            {
                                                //THIS IS A FIX - use hashmap?
                                                if (!hash.ContainsKey(data))
                                                    hash.Add(data, "test");
                                            }
                                            else IsDiscreteValues = false;
                                        }
                                    }
                                    //else bad format line - error message? We're being permissive for now, so allow it.
                                    else
                                    {
                                        Warnings.AppendLine(
                                            string.Format(
                                                "WARNING: Line {0} \"{1}\" not in correct format - header defines {2} columns but this row only has {3} columns",
                                                linecount, line, Headers.Length,fields.Length)
                                        );
                                    }
                                }
                            }
                        }
                    }
                    if (Warnings.Length > 0) return StatusCode.SUCCESS_WITH_WARNINGS;
                    return StatusCode.SUCCESS;
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(LastError))
                    {
                        //something went wrong that hasn't already been handled, so try and provide some information
                        LastError = string.Format("Data loading error: URI={0} Line {1}={2}\n{3}",
                                        URIDataFilename, linecount, line, ex.Message);
                        Warnings.AppendLine("FATAL ERROR: " + LastError);
                    }
                }
                return StatusCode.FAIL;
            }
        }

        /// <summary>
        /// Work out the distribution of data values between the minimum and maximum data values for
        /// a given number of ranges. These ranges are equally distributed between the data min and max.
        /// Pre-condition: must have loaded the data first
        /// </summary>
        /// <param name="NumRanges">The number of data value ranges to partition the data into e.g.
        /// 4 ranges with min=0 and max=100 gives ranges of 0<=x<25, 25<=x<50, 50<=x<75, 75<=x<=100</param>
        /// <returns>An array of counts of the number of data values falling within each range</returns>
        public int[] DataFrequencyDistribution(int NumRanges)
        {
            //todo: you could move this out of the dataloader?
            float BucketWidth = (maxdatavalue - mindatavalue) / ((float)NumRanges);
            int[] buckets = new int[NumRanges];
            for (int i = 0; i < NumRanges; i++) buckets[i] = 0;
            foreach (float value in this.dataset)
            {
                int b = (int)Math.Floor((value - mindatavalue) / BucketWidth);
                if (b < 0) b = 0;
                else if (b >= NumRanges) b = NumRanges - 1;
                ++buckets[b];
            }
            return buckets;
        }
    }
}