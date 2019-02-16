using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.XPath;

using Microsoft.SqlServer.Types;

using GeoAPI.Geometries;
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
//using NetTopologySuite.CoordinateSystems;
//using NetTopologySuite.CoordinateSystems.Transformations;

using MapTubeD.Utilities;

namespace DatastoreMiner
{
    /// <summary>
    /// Build data use to create a set of MapTube maps from a Datastore object
    /// </summary>
    class MapTubeMapBuilder
    {
        const string MapTubeSQLFilename = "MapTubeMaps.sql"; //this is the file that the sql insert data gets written to (it's in the outputDirectory)

        private Datastore datastore = null;
        private string configFilename;
        private string outputDirectory = "";
        private GeometryFinder GF;
        protected Dictionary<string, float> _GeomHints = new Dictionary<string, float>(); //used to pass hints about which geom to pick if there's a tie i.e. LSOA or LSOA_2011 float is weight value

        #region properties
        private string _SequentialScheme = "YlGn";
        public string SequentialScheme
        {
            get { return _SequentialScheme; }
            set { _SequentialScheme = value; }
        }

        private string _DiscreteScheme = "YlGn";
        public string DiscreteScheme
        {
            get { return _DiscreteScheme; }
            set { _DiscreteScheme = value; }
        }

        private string _DivergingScheme = "YlGn";
        public string DivergingScheme
        {
            get { return _DivergingScheme; }
            set { _DivergingScheme = value; }
        }

        private string _baseURL = "/";
        /// <summary>
        /// root of where the maps will be copied to on the server e.g. http://www.maptube.org/census2011
        /// </summary>
        public string BaseURL
        {
            get { return _baseURL; }
            set { _baseURL = value; }
        }

        public void AddGeomHint(string GeometryName,float prob)
        {
            _GeomHints.Add(GeometryName, prob);
        }

        #endregion properties


        public MapTubeMapBuilder(Datastore ds,string configFilename)
        {
            datastore = ds;
            this.configFilename = configFilename;
            GF = new GeometryFinder(configFilename);
        }

        /// <summary>
        /// Build data to allow creation on MapTube.
        /// Output is a set of directories in the outputDirectory, with one for each file source.
        /// Also included is a SQL insert file which can be used to add the maps to the database.
        /// </summary>
        /// <param name="outputDirectory"></param>
        public void Build(string outputDirectory)
        {
            this.outputDirectory = outputDirectory;
            Directory.CreateDirectory(outputDirectory);
            DataTable cat = datastore.DatastoreCatalogue;
            DatastoreSchema schema = datastore.DSSchema;
            int TitleColIdx = cat.Columns.IndexOf(schema.TitleField);
            int LinkColIdx = cat.Columns.IndexOf(schema.LinkField);
            int UniqueKeyColIdx = cat.Columns.IndexOf(schema.UniqueKeyField);

            //delete any existing sql file
            File.Delete(Path.Combine(outputDirectory, MapTubeSQLFilename));

            //todo: check whether you need to create the data staging directory here

            for (int i = 0; i < cat.Rows.Count; i++)
            {
                DataRow Row = cat.Rows[i];
                string Title = Row[TitleColIdx] as string;
                string DataLink = Row[LinkColIdx] as string;
                string UniqueKey = Row[UniqueKeyColIdx] as string; //this is only unique for the table's name and file, so we're going to need to add a column number to this

                if (string.IsNullOrEmpty(DataLink)) continue; //no data so skip

                //Data staging - download to the local file system and unzip if necessary
                Uri StagedDataUri = datastore.StageData(new Uri(DataLink)); //this is either the root of the extracted zip hierarchy, or an actual file
                Uri[] StagedDataFiles = datastore.FilterDataFiles(StagedDataUri); //get a list of files under the staging area that might contain data

                //now get the files and analyse it
                foreach (Uri FileUri in StagedDataFiles)
                {
                    //we should have a true file (not dir) at this point and it should be a valid type as it's been filtered (*.csv)
                    if (FileUri.LocalPath.ToLower().EndsWith(".csv"))
                    {
                        Console.WriteLine("Staged File: " + FileUri.LocalPath);
                        ProcessFile(FileUri, UniqueKey, Title);
                    }
                }
            }
        }

        /// <summary>
        /// Process a csv file, mining the columns for something that we recognise as an area key. If this is satisfied, then we pass the file on to the next procedure
        /// to make maps from the columns given the area key we found here.
        /// </summary>
        /// <param name="CSVUri"></param>
        /// <param name="UniqueKey">Key unique to the table and data file i.e. not column unique</param>
        /// <param name="Title"></param>
        private void ProcessFile(Uri CSVUri, string UniqueKey, string Title)
        {
            //First, the area geometry
            try
            {
                GF.DataRowsToCheck = 200;
                GeometryProbabilities probs = GF.ProbableGeometryFromDataFile(CSVUri);
                string GeometryName;
                int ColumnIndex;
                float prob;
                bool success = probs.GetMax(out GeometryName, out ColumnIndex, out prob);
                if (success)
                {
                    //This is all the geometries that made the threshold
                    var sorted_probs = probs.OrderByDescending(p => p.Value); //sort matched columns by probability values
                    if ((sorted_probs.Count() > 1) && ((sorted_probs.ElementAt(0).Value - sorted_probs.ElementAt(1).Value) <= float.Epsilon))
                    {
                        Console.WriteLine("Area geometry tie - using geom hints");
                        //We have a tie, so use the geometry hints for additional weighting.
                        //Need to create a new GeometryProbabilites object
                        GeometryProbabilities weighted_probs = new GeometryProbabilities();
                        foreach (KeyValuePair<GeometryColumn, float> KVP in probs)
                        {
                            GeometryColumn col = KVP.Key;
                            if (_GeomHints.ContainsKey(col.GeometryName))
                                col.Probability *= _GeomHints[col.GeometryName];
                            weighted_probs.Add(col.GeometryName, col.ColumnIndex, col.ColumnName, col.Probability);
                        }
                        //now switch old probs and new weighted ones
                        probs = weighted_probs;
                        //and then do a re-sort
                        sorted_probs = probs.OrderByDescending(p => p.Value);
                    }
                    foreach (KeyValuePair<GeometryColumn, float> p in sorted_probs)
                    {
                        Console.WriteLine("Area Geometry: " + p.Key.GeometryName + " Column=" + p.Key.ColumnIndex + " probability=" + p.Key.Probability);
                    }
                    probs.GetMax(out GeometryName, out ColumnIndex, out prob);
                    if (prob>0.8) MakeMapsFromColumns(CSVUri,UniqueKey,GeometryName,ColumnIndex, Title);
                }

                //Second, try a point geometry
                //PointGeometryXYCRS PXYCRS = GF.ProbablePointGeometryFromDataFile(CSVUri);
                //if (!string.IsNullOrEmpty(PXYCRS.XFieldName))
                //{
                //    Console.WriteLine("Point Geometry: XField=" + PXYCRS.XFieldName + " YField=" + PXYCRS.YFieldName);
                //    //write out CRS?
                //}
                ////write out the column type information gained from the point data analysis
                //foreach (ColumnType col in GF.ProbablePointDataColumnTypes)
                //{
                //    Console.Write(col.Name);
                //    if (col.IsIndex)
                //    {
                //        Console.Write(" (Index)");
                //    }
                //    else if (col.IsNumeric)
                //    {
                //        Console.Write(" Numeric Min=" + col.Min + " Max=" + col.Max);
                //    }
                //    Console.WriteLine();
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Make maps from every column in a dataset (uri) that can be coded as a numeric. This is performed after determining an area geometry.
        /// </summary>
        /// <param name="CSVUri">Uri of data file (CSV)</param>
        /// <param name="UniqueKey">Table and data file unique key i.e. same for all the columns</param>
        /// <param name="GeometryName">Name of geometry type identified</param>
        /// <param name="ColumnIndex">Index of geometry column</param>
        /// <param name="Title">MapTube title to be used for the maps db insert</param>
        public void MakeMapsFromColumns(Uri CSVUri,string UniqueKey,string GeometryName,int ColumnIndex, string Title)
        {
            string dir = Path.Combine(this.outputDirectory, UniqueKey);
            Directory.CreateDirectory(dir);
            int ColumnNumber = 0;
            PointGeometryXYCRS PXYCRS = GF.ProbablePointGeometryFromDataFile(CSVUri); //trigger test for numeric columns - datamines lines in file
            foreach (ColumnType ctype in GF.ProbablePointDataColumnTypes)
            {
                if (ctype.IsNumeric)
                {
                    string AreaKey = GF.ProbablePointDataColumnTypes[ColumnIndex].Name;
                    string DataField = ctype.Name;
                    string ColumnUniqueKey = UniqueKey + "_" + ColumnNumber;
                    ++ColumnNumber;
                    MapTubeD.DBJoinDescriptorFile desc;
                    MapTubeD.ColourScale cs;
                    MapTubeD.RenderStyle rs;
                    bool Success = CreateMapDescriptor(CSVUri,ColumnUniqueKey,AreaKey,GeometryName,DataField,out desc,out cs,out rs);
                    if (Success)
                    {
                        //write the files - descriptor, style and data
                        string CSVFilename = Path.GetFileNameWithoutExtension(CSVUri.AbsolutePath) + ".csv";
                        desc.DataURL = CSVFilename;
                        desc.RenderStyleURL = ColumnUniqueKey + "_settings.xml";
                        //descriptor
                        using (TextWriter writer = File.CreateText(Path.Combine(dir,ColumnUniqueKey+"_desc.xml")))
                        {
                            writer.WriteLine(desc.SerializeToXML());
                        }
                        //style
                        using (TextWriter writer = File.CreateText(Path.Combine(dir, ColumnUniqueKey + "_settings.xml")))
                        {
                            writer.WriteLine("<gmapcreator>");
                            writer.WriteLine(cs.toXML());
                            writer.WriteLine(rs.toXML());
                            writer.WriteLine("</gmapcreator>");
                        }
                        //data - note that the data is the same for every column found, so we might be writing the same file multiple times here
                        File.Copy(CSVUri.AbsolutePath, Path.Combine(dir,CSVFilename), true);

                        //now the MapTube map information for the maps database entry
                        string DescriptorRelativeUrl = Path.Combine(UniqueKey, ColumnUniqueKey + "_desc.xml");
                        string ColumnTitle = this.datastore.GetTitle(DataField); //override the Title which is just the title of the file, not the variable column
                        string ShortDescription = this.datastore.GetShortDescription(DataField);
                        string Information = this.datastore.GetLongDescription(DataField);
                        string Keywords = this.datastore.GetKeywords(DataField);
                        using (TextWriter writer = File.AppendText(Path.Combine(outputDirectory, MapTubeSQLFilename)))
                        {
                            string sql = CreateSQLInsert(CSVUri, GeometryName, "richard", ColumnTitle, Keywords, ShortDescription, Information, DescriptorRelativeUrl, cs);
                            writer.WriteLine(sql);
                        }

                    }
                }
            }
        }


        /// <summary>
        /// This takes the data for a map and returns the MapTubeD descriptor and settings for it.
        /// </summary>
        public bool CreateMapDescriptor(Uri CSVUri, string UniqueKey, string AreaKey, string GeometryName, string DataField,
            out MapTubeD.DBJoinDescriptorFile desc, out MapTubeD.ColourScale cs, out MapTubeD.RenderStyle rs)
        {
            desc = new MapTubeD.DBJoinDescriptorFile();
            desc.AreaKey = AreaKey;
            desc.Geometry = GeometryName;
            desc.DataField = DataField;
            desc.DataURL = UniqueKey + ".csv";
            desc.RenderStyleURL = UniqueKey + "_settings.xml";

            cs = new MapTubeD.ColourScale();

            //we don't actually do anything with the style at this point, so just create and return a new one
            rs = new MapTubeD.RenderStyle();

            //OK, now we need a style...
            //the rest of this is 'borrowed' from the webmapcreator
            string[] Headers;
            MapTube.GIS.DataLoader loader = new MapTube.GIS.DataLoader();
            MapTube.GIS.DataLoader.StatusCode Success = loader.LoadCSVData(CSVUri.ToString(), DataField, out Headers);
            if (Success != MapTube.GIS.DataLoader.StatusCode.FAIL)
            {
                bool IsDiscreteData = loader.IsDiscreteData;
                if (IsDiscreteData)
                {
                    cs.deleteAllColours();
                    List<float> Values = loader.GetDiscreteValues;
                    Color[] cols = MapTube.GIS.Colours.FindNamedColours(DiscreteScheme, Values.Count);
                    for (int i = 0; i < Values.Count; i++)
                    {
                        cs.addColour(cols[i], Values[i], Convert.ToString(Values[i]));
                    }
                }
                else
                {
                    //For continuous you need an extra colour
                    float[] breaksvalues = null;
                    int NumClass = 5;
                    bool IsMissingData = false;
                    float MissingValue = 0;
                    //breaksvalues = MapTube.GIS.Jenks.GetBreaks(NumClass, IsMissingData, MissingValue, loader.DataSet);
                    breaksvalues = MapTube.GIS.Quantiles.GetBreaks(NumClass, IsMissingData, MissingValue, loader.DataSet);

                    cs.deleteAllColours();
                    //This is a really nasty hack and I would like to get rid of it. If two thresholds in the colourscale are equal,
                    //then their positions flip randomly as the list is re-sorted and the colours and descriptions move.
                    //The only way round this is to have a sortable list that maintains the order colours were added in the event that
                    //two thresholds are equal. Here we get around it by moving the threshold by the smallest possible amount we can
                    //get away with.
                    for (int i = 1; i < breaksvalues.Length; i++)
                    {
                        if (breaksvalues[i - 1] == breaksvalues[i])
                        {
                            //Add small amount to threshold. Remember -ve values and the fact that there are limited bits of precision.
                            breaksvalues[i] = (float)((double)breaksvalues[i] + (Math.Abs((double)breaksvalues[i]) * 0.00000003d));
                        }
                    }
                    //now assign colours the existing colours to the breaks
                    int MissingDataIndex = -1; //might need it in the future - all this comes from the webmapcreator
                    Color[] cols = MapTube.GIS.Colours.FindNamedColours(SequentialScheme, breaksvalues.Length);
                    for (int i = 0; i < breaksvalues.Length; i++)
                    {
                        string text = "";
                        //with a continuous colourscale, a colour only represents a specific data value
                        //text = "Q" + i + " : " + breaksvalues[i];
                        if (i == MissingDataIndex)
                        {
                            text = "Missing Data (x=" + breaksvalues[i] + ")";
                        }
                        //minimum breaks value condition taking into account missing data might be below it (i.e. i=0 or i=1)
                        else if (((i == 0) && (MissingDataIndex == -1)) || ((i == 1) && (MissingDataIndex == 0)))
                        {
                            text = breaksvalues[i] + " (min) <= x < " + breaksvalues[i + 1];
                        }
                        //maximum break taking into account missing data might be above it (NOTE i==MissingDataIndex handled above)
                        else if ((i == breaksvalues.Length - 1) || ((i == breaksvalues.Length - 2) && (MissingDataIndex == i + 1)))
                        {
                            text = "x = " + breaksvalues[i] + " (max)";
                        }
                        //somewhere in the middle case
                        else
                        {
                            text = breaksvalues[i] + " <= x < " + breaksvalues[i + 1];
                        }
                        cs.addColour(cols[i], breaksvalues[i], text);
                    }
                }

                return true;
            }

            return false; //failed, data not valid
        }

        /// <summary>
        /// Get the extents for a MapTubeD geometry.
        /// This needs to be WGS84 extents when the database has Mercator!
        /// </summary>
        /// <param name="GeometryName"></param>
        /// <param name="minlat"></param>
        /// <param name="minlon"></param>
        /// <param name="maxlat"></param>
        /// <param name="maxlon"></param>
        /// <returns></returns>
        public bool GetExtentsForGeometry(string GeometryName,out float minlat, out float minlon, out float maxlat, out float maxlon)
        {
            //According to sharpgis, this is the official webmercator one with EPSG 3785
            //It's also the one used by the latest GMapCreator
            const string GoogleProj =
            "PROJCS[\"Popular Visualisation CRS / Mercator\","
            + "GEOGCS[\"Popular Visualisation CRS\", DATUM[\"Popular Visualisation Datum\","
            + "SPHEROID[\"Popular Visualisation Sphere\", 6378137, 0, AUTHORITY[\"EPSG\",\"7059\"]],"
            + "TOWGS84[0, 0, 0, 0, 0, 0, 0], AUTHORITY[\"EPSG\",\"6055\"]],"
            + "PRIMEM[\"Greenwich\", 0, AUTHORITY[\"EPSG\", \"8901\"]],"
            + "UNIT[\"degree\", 0.0174532925199433, AUTHORITY[\"EPSG\", \"9102\"]],"
            + "AXIS[\"E\", EAST], AXIS[\"N\", NORTH], AUTHORITY[\"EPSG\",\"4055\"]], PROJECTION[\"Mercator\"],"
            + "PARAMETER[\"False_Easting\", 0], PARAMETER[\"False_Northing\", 0], PARAMETER[\"Central_Meridian\", 0],"
            + "PARAMETER[\"Latitude_of_origin\", 0], UNIT[\"metre\", 1, AUTHORITY[\"EPSG\", \"9001\"]],"
            + "AXIS[\"East\", EAST], AXIS[\"North\", NORTH], AUTHORITY[\"EPSG\",\"3785\"]]";

            const string WGS84Proj =
            "GEOGCS[\"GCS_WGS_1984\","
            + "DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137,298.257223563]],"
            + "PRIMEM[\"Greenwich\",0],"
            + "UNIT[\"Degree\",0.0174532925199433]"
            + "]";

            minlat = 0; minlon = 0; maxlat = 0; maxlon = 0;

            string sql = "SELECT {0}.STEnvelope() as [envelope],{1} as [areakey] FROM {2}";

            MapTubeD.GeoDataSources geodb = MapTubeD.GeoDataSources.GetInstance(configFilename);
            
            string ConnectionString, TableName, GeomFieldName, AreaFieldName;
            //MapScale=0 means get the most detailed boundary we have
            bool Success = geodb.GetInfo(GeometryName, 0, out ConnectionString, out TableName, out GeomFieldName, out AreaFieldName);
            if (!Success) return false;

            Envelope MercEnv = new Envelope();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(string.Format(sql, GeomFieldName, AreaFieldName, TableName), conn);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SqlGeometry sqlgeom = reader["envelope"] as SqlGeometry;
                        //TODO: removed bit that determines whether an area key is present before expanding the box - this gets bounds for ALL geometry, not joined geometry
                        //get data value from hash to make sure it exists
                        //String areakey = reader["areakey"] as string;
                        //if (areakey == null) continue;
                        //areakey = areakey.Trim();
                        //Object ob = (MapDescriptor.Data as DBJoin).Data[areakey];
                        //if (ob != null)
                        //{
                            //expand bounds to include data geometry box
                            MercEnv.ExpandToInclude(sqlgeom.STPointN(1).STX.Value, sqlgeom.STPointN(1).STY.Value);
                            MercEnv.ExpandToInclude(sqlgeom.STPointN(3).STX.Value, sqlgeom.STPointN(3).STY.Value);
                        //}
                    }
                }
            }
            //now env contains the bounds in Mercator, we need to convert back to WGS84
            //setup coordinate transformations
            CoordinateSystemFactory CSFactory = new CoordinateSystemFactory();
            ICoordinateSystem CSMerc = CSFactory.CreateFromWkt(GoogleProj);
            ICoordinateSystem CSWGS84 = CSFactory.CreateFromWkt(WGS84Proj);
            CoordinateTransformationFactory ctf = new CoordinateTransformationFactory();
            ICoordinateTransformation trans = ctf.CreateFromCoordinateSystems(CSMerc, CSWGS84);
            Envelope env = NetTopologySuite.CoordinateSystems.Transformations.GeometryTransform.TransformBox(MercEnv, trans.MathTransform);

            double EnvWidth = env.Width;
            //program out error when minx==maxx==-180 for world maps (it wraps wrongly)
            //added a delta minimum for the width, min and max
            if ((EnvWidth <= 0.0000001) && (Math.Abs(env.MinX + 180.0) <= 0.0000001) && (Math.Abs(env.MaxX + 180.0) <= 0.0000001))
            {
                EnvWidth = 360.0;
            }
            minlat = (float)env.MinY;
            minlon = (float)env.MinX;
            maxlat = (float)env.MaxY;
            maxlon = (float)env.MaxX;
            return true;

        }

        /// <summary>
        /// TODO:
        /// Create the SQL string to insert this map directly into the MapTube maps database.
        /// </summary>
        /// <param name="baseUrl">root url of where all the descriptors are going to be copied to e.g. http://www.maptube.org/census2011 </param>
        /// <param name="DescriptorRelativeUrl">Descriptor url relative to the base e.g. UV01EW/UV01EW_01_desc.xml</param>
        /// <returns></returns>
        public string CreateSQLInsert(Uri CSVUri, string GeometryName, string username, string Title, string Keywords, string ShortDescription, string Information, string DescriptorRelativeUrl, MapTubeD.ColourScale cs)
        {
            /*
            CREATE TABLE[dbo].[maps](
            [mapid] [int] IDENTITY(1,1) NOT NULL,
            [username] [nvarchar](24) NULL,
            [mapdescription] [nvarchar](255) NULL,
            [url] [nvarchar](255) NULL,
            [keywords] [nvarchar](255) NULL,
            [maptitle] [nvarchar](64) NULL,
            [tileurl] [nvarchar](255) NULL,
            [maxzoomlevel] [tinyint] NULL,
            [minlat] [float] NULL,
            [minlon] [float] NULL,
            [maxlat] [float] NULL,
            [maxlon] [float] NULL,
            [colourscale] [nvarchar](max) NULL,
            [hits] [int] NULL,
            [creationdate] [datetime] NULL,
            [lastviewdate] [datetime] NULL,
            [information] [nvarchar](max) NULL,
            [maptype] [tinyint] NULL,
            [isclickable] [bit] NOT NULL,
            [timetag] [nvarchar](6) NULL,
            [topicality] [real] NULL
            )
            */
            string SQLPattern = "insert into maps (username,mapdescription,url,keywords,maptitle,tileurl,maxzoomlevel,minlat,minlon,maxlat,maxlon,colourscale,hits,creationdate,lastviewdate,information,maptype,isclickable,timetag,topicality)"
                +" values ('{0}','{1}','{2}','{3}','{4}','{5}',{6},{7},{8},{9},{10},'{11}',{12},'{13}','{14}','{15}',{16},{17},{18},{19});";
            float minlat = 0, minlon = 0, maxlat = 0, maxlon = 0;
            GetExtentsForGeometry(GeometryName, out minlat, out minlon, out maxlat, out maxlon); //note - this does return success or failure...
            //extract the colourThresholds part from the colourscale
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(cs.toXML());
            XmlNode node = doc.SelectSingleNode("/colourscale/colourThresholds");
            string csString = node.OuterXml;

            //todo: data extents and test descriptor url and base url
            string DescriptorUrl = new Uri(Path.Combine(_baseURL, DescriptorRelativeUrl)).ToString();
            string TileUrl = ""; //this could be a pointer to a MapTubeD instance, but nothing uses the default

            //dates need to be in the format 2016-05-15 21:50:00

            return string.Format(SQLPattern, username, ShortDescription, DescriptorUrl, Keywords, Title, TileUrl, 17, minlat, minlon, maxlat, maxlon, csString, 0, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Information, 2, 1,"NULL","NULL");
        }

        public static void RebuildMapImage(int mapid)
        {
            //NOTE: you have to log in using chrome and then ctrl+A+shift and right click to copy the cookie for the .MapTubeSecurity cookie and copy it below
            //http://www.maptube.org/rebuild-thumbnail.aspx?mapid=1371
            //and then you post to it

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://www.maptube.org/rebuild-thumbnail.aspx?mapid="+mapid);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            //request.ContentLength = 0;
            Cookie mycookie = new Cookie(".MapTubeSecurity1",
                "8C547F26F16889344F67ED87F5F94DDB590962F20285074B883E1CC5605AFA5665A92E064FF03A3B84B61BEA961F558AFC26145EAEC640AD243348E25E05166CA2743ECBE7F187D2A82856F89AC27BDD049BA1C1DF5B3CE690762B8C3F3501E28BACAAA6EF5D312C1E8803DC3C640E978EE07C2C01B14C4F5D54B9E1B23DE3664E5FB54359045E8FDA264E3F2070C5E2",
                "/",
                "www.maptube.org");

            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(mycookie);
            //string postBytes = "RecreateImageButton=Rebuild+Image";
            string postBytes =
                "__EVENTTARGET=&__EVENTARGUMENT=&__VIEWSTATE=%2FwEPDwUKLTk3NDQyNzE2MQ9kFgICBA9kFgQCAQ9kFgJmD2QWAmYPDxYCHghJbWFnZVVybAUXbWFwaW1hZ2VzL21hcGlkMTM3My5wbmdkZAIDDw8WAh4EVGV4dGVkZBgBBQpMb2dpblZpZXcxDw9kAgFk%2FnwMIickdQk5HexdZf6Z2YpDzvev2G6%2BUqKZzfTyC8k%3D&__VIEWSTATEGENERATOR=1AAA39E4&__EVENTVALIDATION=%2FwEWAgKknPv0CALX%2BuOSCyTlPNTSVjahUdCulH%2FmGpS4YNuuWuAhALb1h9y%2FTaz4&RecreateImageButton=Rebuild+Image";
            ASCIIEncoding ascii = new ASCIIEncoding();
            byte[] postBytes2 = ascii.GetBytes(postBytes);
            request.ContentLength = postBytes2.Length;
            Stream postStream = request.GetRequestStream();
            postStream.Write(postBytes2, 0, postBytes2.Length);
            postStream.Flush();
            postStream.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            System.Diagnostics.Debug.WriteLine("Response: " + responseString);
            //RecreateImageButton=Rebuild+Image

        }




    }
}
