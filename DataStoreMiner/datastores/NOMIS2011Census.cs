using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;

namespace DatastoreMiner
{
    class NOMIS2011Census : Datastore
    {
        public const string CatalogueFile = "NOMIS2011CensusBulk.csv";
        public const string VariablesFile = "NOMIS2011Variables.txt"; //this is the lookup containing the variable code, statistical unit and text description
        //public Dictionary<string,string> VariableNameDescriptionText;
        DataTable VariableMetaData;

        public override string GetTitle(string VariableName)
        {
            //NOTE: title needs to be no more than 64 chars
            string TableName = VariableName.Substring(0, 7);
            if (TableName.StartsWith("C")) TableName = TableName.Substring(0, 6);

            DataRow TRow = Catalogue.AsEnumerable().Where(t => t.Field<string>("TableName") == TableName).FirstOrDefault();
            string TableDescription = TRow["Description"] as string;

            DataRow VRow = VariableMetaData.Rows.Find(VariableName);
            string VariableText = VRow["ColumnVariableDescription"] as string;

            //string Title = TableDescription + " : " + VariableText; //results in over 1100 which are over 64 chars
            //string Title = VariableText; //results in 327 which are over 64 chars
            //we're going to have to resort to some heuristics
            string Title = VariableText;
            if (Title.Length > 64)
            {
                //heuristic 1, look for colon separated chunks and take the last bit of text before the final colon e.g. Medical and care establishment: Other: Children's home (including secure units)
                int PosColon = Title.LastIndexOf(':');
                if (PosColon > 0) Title = Title.Substring(PosColon + 2);
                //still results in 197 which are over, so heuristic 2, do some semi colon splitting e.g. Managers, directors and senior officials;     Corporate managers and directors;         Health and Social Services Managers and Directors
                if (Title.Length>64)
                {
                    int PosSemi = Title.LastIndexOf(';');
                    if (PosSemi > 0) Title = Title.Substring(PosSemi + 2);

                    //still got 71 which are too long at this point, so going to have to use the final fallback of truncating
                    //heuristic 3, drop words off the end until it's short enough
                    if (Title.Length>64)
                    {
                        while (Title.Length>60)
                        {
                            int LastPos = Title.LastIndexOf(' ');
                            Title = Title.Substring(0, LastPos);
                            Title = Title.Trim();
                        }
                        Title = Title + "...";
                    }
                }
            }
            Title = Title.Trim();

            if (Title.Length > 64) System.Diagnostics.Debug.WriteLine("Error: Title>64 chars: " + Title);

            return Title;
        }

        public override string GetShortDescription(string VariableName)
        {
            //This is a merger between the base table type and the specific column referenced by the unique variable name.
            //The table is extracted from the first part of the variable, with KS and QS 7 digits and CT 6 digits.
            //The text reads as:
            //[VariableText] from the 2011 Census table [TableName] [TableDescription]
            //NOTE: this is limited to 256 chars, but isn't a problem with the way the description is built.
            const string Text = "{0} ({1}) from the 2011 Census table {2} ({3})";

            DataRow VRow = VariableMetaData.Rows.Find(VariableName);
            string VariableText = VRow["ColumnVariableDescription"] as string;

            string TableName = VariableName.Substring(0, 7);
            if (TableName.StartsWith("C")) TableName = TableName.Substring(0, 6);

            //no primary key on catalogue, so have to do it the hard way
            //var TRow = from DataRow myRow in Catalogue.Rows
            //           where (string)myRow["TableName"] == TableName
            //           select myRow;
            //or
            DataRow TRow = Catalogue.AsEnumerable().Where(t => t.Field<string>("TableName") == TableName).FirstOrDefault();
            string TableDescription = TRow["Description"] as string;

            return string.Format(Text, VariableText, VariableName, TableDescription, TableName);
        }

        public override string GetLongDescription(string VariableName)
        {
            //This is a merger between the base table type and the specific column referenced by the unique variable name.
            //The table is extracted from the first part of the variable, with KS and QS 7 digits and CT 6 digits.
            //The data we have is TableName (KS101EW), TableDescripton text (Usual resident pop), Variable (KS101EW001), VariableDescription (Males),
            //OA Url (link to data), ColumnVariableMeasurementUnit (count), ColumnVariableStatisticalUnit (people)
            //URL to NOMIS table description: https://www.nomisweb.co.uk/census/2011/ks101ew and pdf here: https://www.nomisweb.co.uk/census/2011/ks101ew.pdf
            //Bulk download is here: https://www.nomisweb.co.uk/census/2011/bulk/r2_2
            //NOTE: the long description is an unlimited db varchar, so length is not a problem.
            const string Text =
                "<h1>{0} ({1})</h1>"
                +"<ul>"
                + "<li>Table ID: {2}</li>"
                + "<li>Variable ID: {3}</li>"
                + "<li>Source: Census 2011</li>"
                + "<li>Units: {4}</li>"
                + "<li>Keywords: {5}</li>"
                + "<li>Coverage: England and Wales</li>"
                + "<li>Geography: Middle layer super output area (MSOA)</li>"
                + "<li>NOMIS Website: <a href=\"{6}\">{7}</a></li>"
                + "</ul>"
                +"<p><a href=\"{8}\">Download full description (PDF)</a></p>"
                +"<p>Census 2011 data from NOMIS using the field \"{9}\" from the table \"{10}\" ({11})</p>";

            string NOMISWebsite = "https://www.nomisweb.co.uk/census/2011/bulk/r2_2";

            DataRow VRow = VariableMetaData.Rows.Find(VariableName);
            string VariableText = VRow["ColumnVariableDescription"] as string;
            string VariableUnits = VRow["ColumnVariableStatisticalUnit"] as string;

            string TableName = VariableName.Substring(0, 7);
            if (TableName.StartsWith("C")) TableName = TableName.Substring(0, 6);

            string FullDescriptionURL = "https://www.nomisweb.co.uk/census/2011/"+TableName.ToLower()+".pdf";

            DataRow TRow = Catalogue.AsEnumerable().Where(t => t.Field<string>("TableName") == TableName).FirstOrDefault();
            string TableDescription = TRow["Description"] as string;

            string Keywords = VariableText + " " + TableDescription;

            return string.Format(Text,VariableText,VariableName,TableName,VariableName,VariableUnits,Keywords, NOMISWebsite, NOMISWebsite, FullDescriptionURL, VariableText, TableDescription, TableName);
        }

        /// <summary>
        /// Return the keywords for this data
        /// </summary>
        /// <returns></returns>
        public override string GetKeywords(string VariableName)
        {
            DataRow VRow = VariableMetaData.Rows.Find(VariableName);
            string VariableText = VRow["ColumnVariableDescription"] as string;

            string TableName = VariableName.Substring(0, 7);
            if (TableName.StartsWith("C")) TableName = TableName.Substring(0, 6);

            DataRow TRow = Catalogue.AsEnumerable().Where(t => t.Field<string>("TableName") == TableName).FirstOrDefault();
            string TableDescription = TRow["Description"] as string;

            string Keywords = "census 2011 NOMIS r2.2 " + VariableText + " " + TableDescription + " " + VariableName;
            //remove duplicate words and process out colons
            string[] split = Keywords.Split(new char[] { ' ' });
            HashSet<string> words = new HashSet<string>();
            foreach (string s in split) words.Add(s);
            Keywords = "";
            foreach (string s in words)
            {
                if (s.Length < 4) continue;
                string s2 = s.Replace(':', ' ');
                Keywords += s2.ToLower() + " ";
            }

            return Keywords;
        }


        public NOMIS2011Census()
        {
            //define field names on NOMIS website catalogue page that we require for processing
            //TitleField = "Description";
            //LinkField = "oaurl";
            //TagsField = "";
            //DescriptionField = ""; //doesn't exist
            CSVCatalogue reader = new CSVCatalogue();
            this.Catalogue = reader.ReadCatalogue(Path.Combine(DataRootDir,CatalogueFile));
            //FileFilterOptions = new FileFilter(FileFilterEnum.Top, "");
            FileFilterOptions = new FileFilter(FileFilterEnum.Pattern, "DATA.CSV"); //had to change this to prevent returning CODE0.CSV file instead

            //add weights for geometry to favour 2011 datasets over the older ones
            SetGeometryHint("OA_2011", 2.0f); SetGeometryHint("OA", 0.1f);
            SetGeometryHint("LSOA_2011", 2.0f); SetGeometryHint("LSOA", 0.1f);
            SetGeometryHint("MSOA_2011", 2.0f); SetGeometryHint("MSOA", 0.1f);
            
            //then create a schema to describe what the columns are
            Schema = new DatastoreSchema();
            Schema.AddField("TableName", SemanticFieldType.UniqueKey);
            Schema.AddField("Description", SemanticFieldType.Title);
            Schema.AddField("oaurl", SemanticFieldType.Link); //there are two links to data here - oa/lsoa/msoa or wards (below)
            //Schema.AddField("wardurl", SemanticFieldType.Link);

            //Now build a table of description text for every variable using the variables file.
            //This is a quick lookup between variable code and plain text which is used for writing out data file. This is
            //duplicated in the data table loading below.
            //VariableNameDescriptionText = new Dictionary<string, string>();
            //using (TextReader varsFile = File.OpenText(Path.Combine(DataRootDir, VariablesFile)))
            //{
            //    string Line = varsFile.ReadLine(); //skip header
            //    while ((Line = varsFile.ReadLine()) != null)
            //    {
            //        string[] Fields = CSVCatalogue.ParseCSVLine(Line); //need to do this for the quoted final column
            //        //ColumnVariableCode,ColumnVariableMeasurementUnit,ColumnVariableStatisticalUnit,ColumnVariableDescription
            //        //KS101EW0001,Count,Person,All categories: Sex
            //        //KS101EW0002,Count,Person,Males
            //        VariableNameDescriptionText.Add(Fields[0], Fields[3]);
            //    }
            //    varsFile.Close();
            //}

            //This is a full DataTable containing all the data about each individual variable from the variable lookup:
            //ColumnVariableCode,ColumnVariableMeasurementUnit,ColumnVariableStatisticalUnit,ColumnVariableDescription
            //KS101EW0001,Count,Person,All categories: Sex
            //KS101EW0002,Count,Person,Males
            //Used for the short and long description text.
            CSVCatalogue VarCatalogue = new CSVCatalogue();
            VariableMetaData = VarCatalogue.ReadCatalogue(Path.Combine(DataRootDir, VariablesFile));
            VariableMetaData.PrimaryKey = new DataColumn[] { VariableMetaData.Columns["ColumnVariableCode"] };
        }

        /// <summary>
        /// Inherit the base FilterDataFiles function, call it to get the (single) data file at the top of the hierarchy that contains
        /// the data, but which has Country+GOR+LA+MSOA+LSOA+OA rows in it. Then create a new file in the staging area that only has
        /// the OA data in it and return the new filename.
        /// </summary>
        /// <param name="StagedDataUri"></param>
        /// <returns>LSOA file</returns>
        public override Uri[] FilterDataFiles(Uri StagedDataUri)
        {
            //do the normal download, zip extract and return the compound file at the top of the hierarchy
            Uri[] Files = base.FilterDataFiles(StagedDataUri);

            //now let's create a new one: all OA rows start with E00 or W00
            //TODO: you could create all the other files while you're at it
            string OAFilename = Path.Combine(Path.GetDirectoryName(Files[0].LocalPath),Path.GetFileNameWithoutExtension(Files[0].LocalPath)+"_OA.csv");
            string LSOAFilename = Path.Combine(Path.GetDirectoryName(Files[0].LocalPath), Path.GetFileNameWithoutExtension(Files[0].LocalPath) + "_LSOA.csv");
            string MSOAFilename = Path.Combine(Path.GetDirectoryName(Files[0].LocalPath), Path.GetFileNameWithoutExtension(Files[0].LocalPath) + "_MSOA.csv");
            string WardFilename = Path.Combine(Path.GetDirectoryName(Files[0].LocalPath), Path.GetFileNameWithoutExtension(Files[0].LocalPath) + "_Ward.csv");
            using (TextReader reader = File.OpenText(Files[0].LocalPath))
            {
                TextWriter writerOA = File.CreateText(OAFilename);
                TextWriter writerLSOA = File.CreateText(LSOAFilename);
                TextWriter writerMSOA = File.CreateText(MSOAFilename);
                TextWriter writerWard = File.CreateText(WardFilename);
                try
                {   
                    //read header line and write it back out
                    string Line = reader.ReadLine();
                    writerOA.WriteLine(Line);
                    writerLSOA.WriteLine(Line);
                    writerMSOA.WriteLine(Line);
                    writerWard.WriteLine(Line);

                    //now the rest of the file
                    while ((Line = reader.ReadLine()) != null)
                    {
                        //this is the filter - E00 or W00 are England and Wales OA codes
                        if (Line.StartsWith("E00")||Line.StartsWith("W00"))
                            writerOA.WriteLine(Line);
                        //E01 and W01 are LSOA
                        else if (Line.StartsWith("E01")||Line.StartsWith("W01"))
                            writerLSOA.WriteLine(Line);
                        //E02 and W02 are MSOA
                        else if (Line.StartsWith("E02") || Line.StartsWith("W02"))
                            writerMSOA.WriteLine(Line);
                        //E05 and W05 are Wards
                        else if (Line.StartsWith("E05") || Line.StartsWith("W05"))
                            writerWard.WriteLine(Line);
                    }
                    
                }
                finally
                {
                    writerOA.Close();
                    writerLSOA.Close();
                    writerMSOA.Close();
                    writerWard.Close();
                }
                reader.Close();
            }
            //return LSOA file here (OA, MSOA options commented out)
            //HACK! switched this to return MSOA filename
            return new Uri [] { /*new Uri(OAFilename)*/ /*new Uri(LSOAFilename)*/ new Uri(MSOAFilename) /*new Uri(WardFilename)*/ };
        }

        /// <summary>
        /// Most of this code comes from AnalyseCorrelationData, but it returns a mapping between the dataset index and a plain text name containing the dataset table and variable
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, string> GetDescriptionForIndex()
        {
            Dictionary<int, string> Result = new Dictionary<int, string>();

            //load mapping between unique dataset field code and plain text description into hash
            Dictionary<string, string> variables = new Dictionary<string, string>();
            using (TextReader varsFile = File.OpenText(Path.Combine(DataRootDir, "NOMIS2011Variables.txt")))
            {
                string Line = varsFile.ReadLine(); //skip header
                while ((Line = varsFile.ReadLine()) != null)
                {
                    string[] Fields = CSVCatalogue.ParseCSVLine(Line); //need to do this for the quoted final column
                    //ColumnVariableCode,ColumnVariableMeasurementUnit,ColumnVariableStatisticalUnit,ColumnVariableDescription
                    //KS101EW0001,Count,Person,All categories: Sex
                    //KS101EW0002,Count,Person,Males
                    variables.Add(Fields[0], Fields[3]);
                }
                varsFile.Close();
            }

            //load mapping between major/minor index and the unique column code
            //I'm not actually using the two index dictionaries, but keep it in anyway
            Dictionary<string, string> indexToFieldName = new Dictionary<string, string>();
            Dictionary<string, string> indexToTableName = new Dictionary<string, string>();
            using (TextReader mapIndexFile = File.OpenText(Path.Combine(ImageDirectory, "mapindex.csv")))
            {
                string Line = mapIndexFile.ReadLine(); //skip header
                int index = 0;
                while ((Line = mapIndexFile.ReadLine()) != null)
                {
                    //major_index,minor_index,data_uri,uniquekey,title,column
                    //0,0,"file:///c:/richard/wxtemp/Datastores/ks101ew_2011_oa/ks101ew_2011oa/KS101EWDATA_LSOA.csv","KS101EW","Usual Resident Population","KS101EW0001"
                    string[] Fields = CSVCatalogue.ParseCSVLine(Line);
                    indexToFieldName.Add(Fields[0] + "-" + Fields[1], Fields[5]);
                    indexToTableName.Add(Fields[0] + "-" + Fields[1], Fields[4]);
                    //Result.Add(Fields[0] + "-" + Fields[1], Fields[5] + " " + Fields[4]);
                    Result.Add(index, Fields[5] + " " + Fields[4]+" "+variables[Fields[5]]);
                    ++index;
                }
                mapIndexFile.Close();
            }

            return Result;
        }

        /// <summary>
        /// Load the NOMIS variables file, mapindex.csv file and imatch-sorted.csv file and write out plain text descriptions of everything that we think matches.
        /// TODO: need some sort of datastore neutral way of doing this for everything, not just NOMIS
        /// </summary>
        public void AnalyseCorrelationData()
        {
            //load mapping between unique dataset field code and plain text description into hash
            Dictionary<string, string> variables = new Dictionary<string, string>();
            using (TextReader varsFile = File.OpenText(Path.Combine(DataRootDir, "NOMIS2011Variables.txt")))
            {
                string Line = varsFile.ReadLine(); //skip header
                while ((Line = varsFile.ReadLine()) != null)
                {
                    string[] Fields = CSVCatalogue.ParseCSVLine(Line); //need to do this for the quoted final column
                    //ColumnVariableCode,ColumnVariableMeasurementUnit,ColumnVariableStatisticalUnit,ColumnVariableDescription
                    //KS101EW0001,Count,Person,All categories: Sex
                    //KS101EW0002,Count,Person,Males
                    variables.Add(Fields[0], Fields[3]);
                }
                varsFile.Close();
            }

            //load mapping between major/minor index and the unique column code
            Dictionary<string, string> indexToFieldName = new Dictionary<string, string>();
            Dictionary<string, string> indexToTableName = new Dictionary<string, string>();
            using (TextReader mapIndexFile = File.OpenText(Path.Combine(ImageDirectory, "mapindex.csv")))
            {
                string Line = mapIndexFile.ReadLine(); //skip header
                while ((Line = mapIndexFile.ReadLine()) != null)
                {
                    //major_index,minor_index,data_uri,uniquekey,title,column
                    //0,0,"file:///c:/richard/wxtemp/Datastores/ks101ew_2011_oa/ks101ew_2011oa/KS101EWDATA_LSOA.csv","KS101EW","Usual Resident Population","KS101EW0001"
                    string[] Fields = CSVCatalogue.ParseCSVLine(Line);
                    indexToFieldName.Add(Fields[0] + "-" + Fields[1], Fields[5]);
                    indexToTableName.Add(Fields[0] + "-" + Fields[1], Fields[4]);
                }
                mapIndexFile.Close();
            }

            //now read the data and write out plain text descriptions of the matches that are found
            using (TextReader matchFile = File.OpenText(Path.Combine(ImageDirectory,"GreenMatch\\imatch-sorted.csv")))
            {
                //imajor, iminor, jmajor, jminor, value (would have i,j and two filenames, but had to remove them as the csvfix sort required too much memory)
                //0,1,10,1,5.90647686550526
                string Line = "";
                while ((Line = matchFile.ReadLine()) != null)
                {
                    string[] Fields = CSVCatalogue.ParseCSVLine(Line);
                    //int imajor = Convert.ToInt32(Fields[0]);
                    //int iminor = Convert.ToInt32(Fields[1]);
                    //int jmajor = Convert.ToInt32(Fields[2]);
                    //int jminor = Convert.ToInt32(Fields[3]);
                    float value = Convert.ToSingle(Fields[4]);
                    if (value > 20.0f) break; //it's a sorted list and 20 is just about on the first knee of the curve

                    string I = Fields[0] + "-" + Fields[1];
                    string J = Fields[2] + "-" + Fields[3];
                    if (I != J) //filter out everything matching itself
                    {
                        string ITable = indexToTableName[I]; //get the names of the tables where the data comes from using the major/minor indexes
                        string JTable = indexToTableName[J];
                        string IColumn = indexToFieldName[I]; //get unique column codes from major/minor map numbers
                        string JColumn = indexToFieldName[J];
                        string IText = variables[IColumn]; //use the two unique column codes to lookup the text descriptions
                        string JText = variables[JColumn];
                        System.Diagnostics.Debug.WriteLine(value + "," + IColumn + "," + JColumn + ",\"(" + ITable + ") " + IText + " AND (" + JTable + ") " + JText+"\"");
                    }
                }
            }
        }

        /// <summary>
        /// Virtual to upload all NOMIS data to a database
        /// </summary>
        /// <param name="ConnectionString"></param>
        public void UploadDatabase(string ConnectionString)
        {

        }

        /// <summary>
        /// Read in a raw input file and filter out any bad or blank lines.
        /// At the same time, make a note of index numbers which we can check against the master list for any
        /// missing correlation values. Also, look for duplicates and errors line NaN.
        /// And, keep track of the millisecond timer. When it goes down, write out the previous max and number of correlations.
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="OutFilename"></param>
        public static void PostProcessClean(string InFilename, string OutFilename)
        {
            HashSet<string> UniqueVariables = new HashSet<string>(); //holds unique i, col_i (plus the j version, but only 2558)
            Dictionary<string,int> TablesHistogram = new Dictionary<string,int>(); //holds count of matches against each unique table
            HashSet<string> NaNTables = new HashSet<string>(); //holds failed correlation tables
            HashSet<string> Variables = new HashSet<string>(); //holds unique i, j, col_i, col_j hash
            string Line;
            int totalcount=0, count = 0; //total count is number of unique lines written, count is for the corrs/second calculation
            long baseTime = 0, lastTime=0; //used for corrs/second
            using (TextReader reader = File.OpenText(InFilename))
            {
                using (TextWriter writer = File.CreateText(OutFilename))
                {
                    while ((Line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(Line)) continue;
                        string[] Fields = Line.Split(new char[] { ',' });
                        //Correlate,24.5385134706347,0.977720119272712,0,0,1,3,KS101EWDATA_MSOA.csv,KS101EWDATA_MSOA.csv,KS101EW0001,KS101EW0003,7201,7201,7201,4706
                        if (Fields.Length != 15) continue;
                        if (Fields[0] != "Correlate") continue;
                        if ((Fields[1] == "NaN") || (Fields[2] == "NaN"))
                        {
                            System.Diagnostics.Debug.WriteLine("NaN: " + Line);
                            NaNTables.Add(Fields[7] + "_" + Fields[8]);
                            continue;
                        }
                        int i, j, Col_i, Col_j;
                        i=Convert.ToInt32(Fields[3]);
                        j=Convert.ToInt32(Fields[4]);
                        Col_i=Convert.ToInt32(Fields[5]);
                        Col_j=Convert.ToInt32(Fields[6]);
                        string keyi = i + "_" + Col_i;
                        string keyj = j + "_" + Col_j;
                        UniqueVariables.Add(keyi);
                        UniqueVariables.Add(keyj);
                        string key = keyi + "_" + keyj;
                        string rkey = keyj + "_" + keyi;
                        if (Variables.Contains(key))
                        {
//                            System.Diagnostics.Debug.WriteLine("Duplicate: " + key);
                            //you could hold the correlation values and check with the duplicate
                        }
                        else if (Variables.Contains(rkey))
                        {
//                            System.Diagnostics.Debug.WriteLine("Duplicate reverse key: " + rkey);
                        }
                        else
                        {
                            writer.WriteLine(
                                "{0},{1},{2},{3},{4},{5},{6},{7}",
                                Fields[1], Fields[2], Fields[3], Fields[4], Fields[5], Fields[6], Fields[9], Fields[10]
                            );
                            Variables.Add(key);
                            ++totalcount;
                            //update count of matches per table
                            //use 7,8 for table filenames or 9,10 for variable names
                            if (TablesHistogram.ContainsKey(Fields[7])) TablesHistogram[Fields[7]] = TablesHistogram[Fields[7]] + 1;
                            else TablesHistogram[Fields[7]] = 1;
                            if (TablesHistogram.ContainsKey(Fields[8])) TablesHistogram[Fields[8]] = TablesHistogram[Fields[8]] + 1;
                            else TablesHistogram[Fields[8]] = 1;
                        }
                        long ms = Convert.ToInt64(Fields[14]);
                        if (ms < (lastTime-30000)) //fiddle for out of order completion on multiple cores
                        {
                            if (lastTime > baseTime)
                            {
                                float CorrPerSec = (float)count / (float)(lastTime - baseTime) * 1000.0f;
                                System.Diagnostics.Debug.WriteLine("Timing:, " + CorrPerSec+", "+count);
                            }
                            else System.Diagnostics.Debug.WriteLine("Error: zero count time");
                            baseTime = ms;
                            count = 0;
                        }
                        lastTime = ms;
                        ++count;
                    }
                }
            }
            foreach (string key in UniqueVariables) System.Diagnostics.Debug.WriteLine(key);
            foreach (string key in NaNTables) System.Diagnostics.Debug.WriteLine(key);
            foreach (KeyValuePair<string,int> KVP in TablesHistogram) System.Diagnostics.Debug.WriteLine(KVP.Key+","+KVP.Value);
            System.Diagnostics.Debug.WriteLine("Finished: TotalCount="+totalcount+" HashCount="+Variables.Count+" UniqueCount="+UniqueVariables.Count);
        }

        /// <summary>
        /// Post process on the "fast" data, which is in a different csv format and in multiple files.
        /// Same code as the non-fast version, but modified for the different csv format.
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="OutFilename"></param>
        public void PostProcessCleanFast(string [] InFilename, string OutFilename)
        {
            HashSet<string> UniqueVariables = new HashSet<string>(); //holds unique i, col_i (plus the j version, but only 2558)
            Dictionary<string, int> TablesHistogram = new Dictionary<string, int>(); //holds count of matches against each unique table
            HashSet<string> NaNTables = new HashSet<string>(); //holds failed correlation tables
            HashSet<string> Variables = new HashSet<string>(); //holds unique i, j, col_i, col_j hash
            string Line;
            int totalcount = 0, count = 0; //total count is number of unique lines written, count is for the corrs/second calculation
            long baseTime = 0, lastTime = 0; //used for corrs/second
            float maxI = -1, minI=1;
            using (TextWriter writer = File.CreateText(Path.Combine(ImageDirectory, OutFilename)))
            {
                foreach (string Filename in InFilename)
                {
                    string PathFilename = Path.Combine(ImageDirectory, Filename);
                    using (TextReader reader = File.OpenText(PathFilename))
                    {
                        reader.ReadLine(); //skip header line
                        while ((Line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrEmpty(Line)) continue;
                            string[] Fields = Line.Split(new char[] { ',' });
                            //I,i,j,VarName_i,VarName_j,milliseconds
                            //0.999357636625448,0,0,KS101EW0001,KS101EW0001,766
                            //0.987952788436483,0,1,KS101EW0001,KS101EW0002,1527
                            if (Fields.Length != 6) continue;
                            if (Fields[0] == "NaN")
                            {
                                System.Diagnostics.Debug.WriteLine("NaN: " + Line);
                                NaNTables.Add(Fields[3] + "_" + Fields[4]);
                                continue;
                            }
                            float I = Convert.ToSingle(Fields[0]);
                            if (I < minI) minI = I;
                            if (I > maxI) maxI = I;
                            int i, j;
                            i = Convert.ToInt32(Fields[1]);
                            j = Convert.ToInt32(Fields[2]);
                            string tablei = Fields[3].Substring(0, Fields[3].Length - 6); //strip off the EW0001
                            string tablej = Fields[4].Substring(0, Fields[4].Length - 6); //strip off the EW0001
                            string keyi = Convert.ToString(i);
                            string keyj = Convert.ToString(j);
                            UniqueVariables.Add(keyi);
                            UniqueVariables.Add(keyj);
                            string key = keyi + "_" + keyj;
                            string rkey = keyj + "_" + keyi;
                            if (Variables.Contains(key))
                            {
                                //                            System.Diagnostics.Debug.WriteLine("Duplicate: " + key);
                                //you could hold the correlation values and check with the duplicate
                            }
                            else if (Variables.Contains(rkey))
                            {
                                //                            System.Diagnostics.Debug.WriteLine("Duplicate reverse key: " + rkey);
                            }
                            else
                            {
                                writer.WriteLine(
                                    "{0},{1},{2},{3},{4},{5}",
                                    Fields[0], Fields[1], Fields[2], Fields[3], Fields[4], Fields[5]
                                );
                                Variables.Add(key);
                                ++totalcount;
                                //update count of matches per table
                                //use 7,8 for table filenames or 9,10 for variable names
                                if (TablesHistogram.ContainsKey(tablei)) TablesHistogram[tablei] = TablesHistogram[tablei] + 1;
                                else TablesHistogram[tablei] = 1;
                                if (TablesHistogram.ContainsKey(tablej)) TablesHistogram[tablej] = TablesHistogram[tablej] + 1;
                                else TablesHistogram[tablej] = 1;
                            }
                            long ms = Convert.ToInt64(Fields[5]);
                            if (ms < (lastTime - 30000)) //fiddle for out of order completion on multiple cores
                            {
                                if (lastTime > baseTime)
                                {
                                    float CorrPerSec = (float)count / (float)(lastTime - baseTime) * 1000.0f;
                                    System.Diagnostics.Debug.WriteLine("Timing:, " + CorrPerSec + ", " + count);
                                }
                                else System.Diagnostics.Debug.WriteLine("Error: zero count time");
                                baseTime = ms;
                                count = 0;
                            }
                            lastTime = ms;
                            ++count;
                        }
                    }
                }
            }
            foreach (string key in UniqueVariables) System.Diagnostics.Debug.WriteLine(key);
            foreach (string key in NaNTables) System.Diagnostics.Debug.WriteLine(key);
            foreach (KeyValuePair<string, int> KVP in TablesHistogram) System.Diagnostics.Debug.WriteLine(KVP.Key + "," + KVP.Value);
            System.Diagnostics.Debug.WriteLine("Finished: TotalCount=" + totalcount + " HashCount=" + Variables.Count + " UniqueCount=" + UniqueVariables.Count);
            System.Diagnostics.Debug.WriteLine("minI=" + minI + " maxI=" + maxI);

        }

        /// <summary>
        /// Post process the k nearest neighbour data by removing all the blank lines
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="OutFilename"></param>
        public static void PostProcessCleanKNN(string InFilename, string OutFilename)
        {
            using (TextReader reader = File.OpenText(InFilename))
            {
                using (TextWriter writer = File.CreateText(OutFilename))
                {
                    string Line;
                    while ((Line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(Line)) continue;
                        writer.WriteLine(Line);
                    }
                }
            }
        }

        /// <summary>
        /// For each table, generate a relationship measure to every other table based on I values.
        /// This acts on the processed datafile.
        /// What it does is to aggregate all the correlations between every pair of tables by averaging the individual
        /// variable correlations.
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="Lookup">Mapping between column number and unique table name</param>
        /// <param name="matrix">A matrix of all the correlation values</param>
        public static void PostProcessGenerateTableRelationships(
            string InFilename, string OutFilename,
            out Dictionary<int,string> Lookup, out float [,] matrix)
        {
            const int NumI = 104; //at this point we know the number of tables (datasets)
            Lookup = new Dictionary<int, string>(); //build lookup between i and table name

            int TotalCount=0, PlusCount = 0, MinusCount = 0; //counts plus and minus correlations for statistics

            float[,] Sums = new float[NumI,NumI];
            float[,] Counts = new float[NumI,NumI];

            using (TextReader reader = File.OpenText(InFilename))
            {
                string Line;
                while ((Line = reader.ReadLine()) != null)
                {
                    string[] Fields = Line.Split(new char[] { ',' });
                    float I2;
                    int i, j, Col_i, Col_j;
                    I2 = Convert.ToSingle(Fields[1]);
                    i = Convert.ToInt32(Fields[2]);
                    j = Convert.ToInt32(Fields[3]);
                    Col_i = Convert.ToInt32(Fields[4]);
                    Col_j = Convert.ToInt32(Fields[5]);
                    //symmetric write
                    Sums[i,j] += I2; ++Counts[i,j];
                    Sums[j,i] += I2; ++Counts[j,i];
                    //counts
                    ++TotalCount;
                    if (I2 > 0) ++PlusCount;
                    else if (I2 < 0) ++MinusCount;
                    if (!Lookup.ContainsKey(i))
                    {
                        string CensusFileName = Fields[7]; //e.g. KS101EWDATA_MSOA.csv
                        string TableName = CensusFileName.Substring(0, 5); //they are all 5 letters by definition
                        Lookup.Add(i, TableName);
                    }
                }
            }
            //write out final aggregate averages
            matrix = new float[NumI,NumI];
            using (TextWriter writer = File.CreateText(OutFilename))
            {
                //header line
                writer.Write("i,table");
                for (int i = 0; i < NumI; i++) writer.Write("," + Lookup[i]);
                writer.WriteLine();
                //now the data lines
                for (int i = 0; i < NumI; i++)
                {
                    writer.Write(i+","+Lookup[i]);
                    for (int j = 0; j < NumI; j++)
                    {
                        matrix[i, j] = Sums[i, j] / Counts[i, j];
                        writer.Write("," + (Sums[i, j] / Counts[i, j]));
                    }
                    writer.WriteLine();
                }
            }
            System.Diagnostics.Debug.WriteLine("TotalCount=" + TotalCount + " PlusCount=" + PlusCount + " MinusCount=" + MinusCount);

            //hack! write out a flattened list of I,i,j so that we can generate a histogram
            //for (int i = 0; i < NumI; i++)
            //{
            //    for (int j = 0; j < NumI; j++)
            //        System.Diagnostics.Debug.WriteLine((Sums[i, j] / Counts[i, j])+","+i+","+j+","+Lookup[i]+","+Lookup[j]);
            //}
        }

    }
}
