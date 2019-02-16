using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Security.AccessControl;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

//PM> Install-Package Npgsql
using Npgsql;

using GeoAPI;
using GeoAPI.Geometries;
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using NetTopologySuite;
using NetTopologySuite.Geometries;
//using NetTopologySuite.CoordinateSystems;
//using NetTopologySuite.CoordinateSystems.Transformations;
using SharpMap.Data.Providers;


//PM> Install-Package ICSharpCode.SharpZipLib.dll
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
//using ICSharpCode.SharpZipLib.GZip;
//using ICSharpCode.SharpZipLib.BZip2;
//using ICSharpCode.SharpZipLib.Tar;

using MapTubeD.Utilities;

namespace DatastoreMiner
{
    /// <summary>
    /// Base class for Datastore data. Subclass for LondonDatastore or GovDatastore
    /// </summary>
    class Datastore
    {
        public const string DataRootDir = "..\\..\\..\\Data\\"; //this is actually the catalogue files
        public const string DataStagingDir = "c:\\richard\\wxtemp\\Datastores\\"; //this is where data gets downloaded to before making maps
        //ideally, you wouldn't be using the local web server's copy
        //protected const string ConfigFilename = "c:\\inetpub\\wwwroot\\MapTube\\App_Data\\geodatasources.xml";
        protected const string ConfigFilename = "c:\\inetpub\\wwwroot\\App_Data\\geodatasources.xml";
        //protected const string StopWordsFilename = "glasgow_stop_words.txt";
        protected const string StopWordsFilename = "glasgow_stop_words_mod.txt"; //added some additional words to this
        protected const string ReportFile = "classification.txt"; //plain text report of what happened
        protected const string MapIndexFile = "mapindex.csv"; //csv file linking map id numbers to files and column headings
        protected const string ImageMatchReportFile = "imatch.txt";
        protected DataTable Catalogue = null;
        protected DatastoreSchema Schema = null;
        protected FileFilter FileFilterOptions = new FileFilter(FileFilterEnum.Top,""); //default to returning the top file in an extracted archive of data

        protected string MSOAShapefile = "..\\..\\..\\Data\\ONS\\MSOA_2011_EW_BGC_V2.shp";

        protected string _ImageDirectory = "..\\..\\..\\Data\\images\\";
        protected Dictionary<string,float> _GeomHints = new Dictionary<string,float>(); //used to pass hints about which geom to pick if there's a tie i.e. LSOA or LSOA_2011 float is weight value

        //these are names of fields in the catalogue file that we need for processing
        //protected string TitleField = ""; //dataset title
        //protected string LinkField = ""; //column containing http address of data
        //protected string TagsField = "";
        //protected string DescriptionField = ""; //column containing a plain text description - used to make keyword links between maps

        //protected int _DatasetCount = -1; //total number of datasets
        //protected int _CSVLinkCount = -1; //number of datasets containing csv links
        //protected int _ZIPLinkCount = -1; //number of datasets containing zip or gzip links
        protected int _AreaGeometryCount = 0; //number of datasets recognised as area geometry
        protected int _PointGeometryCount = 0; //number of datasets recognised as point geometry

        //colour schemes used to draw maps
        protected const string DiscreteScheme = "YlGn"; //was Set3
        protected const string SequentialScheme = "YlGn"; //was OrRd

        /// <summary>
        /// Constructor for datastore class
        /// </summary>
        public Datastore()
        {
            //this.ImageDirectory = ImageDir;
        }

        #region properties

        /// <summary>
        /// Directory where image map files will be stored. Defaults to ..\\..\\Data\\images\\
        /// </summary>
        public string ImageDirectory
        {
            get { return _ImageDirectory; }
            set { _ImageDirectory = value; }
        }

        public DataTable DatastoreCatalogue
        {
            get
            {
                return Catalogue;
            }
        }

        public DatastoreSchema DSSchema
        {
            get
            {
                return Schema;
            }
        }

        /// <summary>
        /// Number of rows in the catalogue file
        /// </summary>
        public int DatasetCount
        {
            get
            {
                return Catalogue.Rows.Count;
            }
        }

        /// <summary>
        /// Links in the catalogue file that point to CSV files
        /// </summary>
        public int CSVLinkCount
        {
            get
            {
                int _CSVLinkCount = 0;
                int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
                foreach (DataRow Row in Catalogue.Rows)
                {
                    string Link = Row[LinkColIdx] as string;
                    if (!string.IsNullOrEmpty(Link))
                    {
                        Link = Link.ToLower();
                        if (Link.EndsWith(".csv"))
                            ++_CSVLinkCount;
                    }
                }
                return _CSVLinkCount;
            }
        }

        /// <summary>
        /// Links in the catalogue file that point to ZIP files (either .zip or .gz)
        /// </summary>
        public int ZIPLinkCount
        {
            get
            {
                int _ZIPLinkCount = 0;
                int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
                foreach (DataRow Row in Catalogue.Rows)
                {
                    string Link = Row[LinkColIdx] as string;
                    if (!string.IsNullOrEmpty(Link))
                    {
                        Link = Link.ToLower();
                        if (Link.EndsWith(".zip"))
                            ++_ZIPLinkCount;
                        else if (Link.EndsWith(".gz"))
                            ++_ZIPLinkCount;
                    }
                }
                return _ZIPLinkCount;
            }
        }

        #endregion

        public virtual string GetTitle(string UniqueKey)
        {
            return "Title for " + UniqueKey;
        }

        /// <summary>
        /// Overloaded in specific datastore types to allow a short plain text description of a map variable to be returned.
        /// Used for the MapTube entry. Returns about a 30 word description.
        /// </summary>
        /// <param name="UniqueKey"></param>
        /// <returns></returns>
        public virtual string GetShortDescription(string UniqueKey)
        {
            return "Description for " + UniqueKey;
        }

        /// <summary>
        /// Overloaded in specific datastore types to allow a fully detailed long plain text description of a map variable to be returned.
        /// Used for the MapTube entry. Can return hundreds of words if that level of detail is required.
        /// </summary>
        /// <param name="UniqueKey"></param>
        /// <returns></returns>
        public virtual string GetLongDescription(string UniqueKey)
        {
            return "Description for " + UniqueKey;
        }

        /// <summary>
        /// Keywords used for MapTube.
        /// </summary>
        /// <param name="UniqueKey"></param>
        /// <returns></returns>
        public virtual string GetKeywords(string UniqueKey)
        {
            return "Keywords for " + UniqueKey;
        }

        /// <summary>
        /// Set an additional weighting when looking for matching geometry. For example, LSOA and LSOA_2011 look the same, but with a hint of LSOA_2011=2.0, this will always
        /// be picked in preference to the other geometry dataset.
        /// </summary>
        /// <param name="GeomName"></param>
        /// <param name="Weight"></param>
        public void SetGeometryHint(string GeomName, float Weight)
        {
            if (!_GeomHints.ContainsKey(GeomName))
                _GeomHints.Add(GeomName, Weight);
            else
                _GeomHints[GeomName] = Weight;
        }


        #region data staging

        /// <summary>
        /// From the MSDN example to decompress a file
        /// BUT WON'T DECOMPRESS A ZIP CONTAINING MULTIPLE FILES
        /// </summary>
        /// <param name="fileToDecompress"></param>
        //public static void Decompress(FileInfo fileToDecompress)
        //{
        //    using (FileStream originalFileStream = fileToDecompress.OpenRead())
        //    {
        //        string currentFileName = fileToDecompress.FullName;
        //        string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);
                
        //        using (FileStream decompressedFileStream = File.Create(newFileName))
        //        {
        //            using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
        //            {
        //                //CopyTo only in .net 4
        //                //decompressionStream.CopyTo(decompressedFileStream);
                        
        //                //CopyTo the hard way - in and out in 10K chunks until we're done
        //                byte[] buf = new byte[10240];
        //                int read = 0;
        //                do
        //                {
        //                    read = decompressionStream.Read(buf, 0, buf.Length);
        //                    decompressedFileStream.Write(buf, 0, read);
        //                } while (read > 0);

        //                Console.WriteLine("Decompressed: {0}", fileToDecompress.Name);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// From the ICSharpCode SharpZipLib examples
        /// </summary>
        /// <param name="fileToDecompress"></param>
        /// <returns>The directory that we decompressed to</returns>
        public static string Decompress(FileInfo fileToDecompress)
        {
            string outFolder = Path.Combine(fileToDecompress.DirectoryName, Path.GetFileNameWithoutExtension(fileToDecompress.Name));
            ZipFile zf = null;
            try
            {
                FileStream fs = File.OpenRead(fileToDecompress.FullName);
                zf = new ZipFile(fs);
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;           // Ignore directories
                    }
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
            return outFolder;
        }


        /// <summary>
        /// Method to download a copy of the data to the local machine before making a map. If the file
        /// downloaded is a zip archive, then the contents are extracted and a Uri pointing to the root
        /// of the extraction folder hierarchy is returned.
        /// This could be overridden to provide downloading of additiona data formats, but a better
        /// pattern is to make this as robust as possible.
        /// </summary>
        /// <param name="DataUri">Remote location to load the data from</param>
        /// <returns>The local Uri where the data is now being staged</returns>
        public virtual Uri StageData(Uri DataUri)
        {
            string Name = Path.GetFileName(DataUri.LocalPath);
            string LocalStagingFile = Path.Combine(DataStagingDir, Name);
            //Test existence of file here and short circuit downloading code if it already exists. This is mainly
            //for debugging so you don't need to download the file every single time you run it. This allows you to build
            //up the list of raw data files on the local machine one by one.
            if (!File.Exists(LocalStagingFile))
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(DataUri, LocalStagingFile);
                }
            }

            //now see what we've downloaded and take appropriate action
            string Extension = Path.GetExtension(DataUri.LocalPath).ToLower();
            if (Extension == ".zip")
            {
                //do the decompress and change the LocalStagingFile to the newly decompressed directory
                LocalStagingFile = Decompress(new FileInfo(LocalStagingFile));
            }
            //else if (Extension == ".gz") { }
            //etc

            return new Uri(LocalStagingFile);
        }

        /// <summary>
        /// Clear the staging area of any data previously downloaded
        /// </summary>
        public void DeleteStagedData()
        {
            //This is really dangerous if the DataStagingDir is ever wrong
            DirectoryInfo di = new DirectoryInfo(DataStagingDir);
            foreach (FileInfo fi in di.GetFiles("*.*", SearchOption.AllDirectories))
            {
                File.Delete(fi.FullName);
            }
        }

        /// <summary>
        /// Filter function used to operate on the Uri returned from StageData. This could be trivial in the case
        /// of a single downloaded data file (return the file), or in the case of a ZIP archive that has been
        /// decompressed, it will look though all the files and return a list of any matching a specific pattern.
        /// Pattern can be:
        /// biggest/smallest, deepest/topmost (dir hierarchy), filepattern regex, or all of them 
        /// </summary>
        /// <param name="StagedDataUri"></param>
        /// <returns>A list of interesting files that might contain data</returns>
        public virtual Uri[] FilterDataFiles(Uri StagedDataUri)
        {
            List<Uri> Result = new List<Uri>();
            FileAttributes attrib = File.GetAttributes(StagedDataUri.LocalPath);
            if ((attrib & FileAttributes.Normal)!=0) //plain file i.e. not a directory
            {
                Result.Add(StagedDataUri);
            }
            else
            {
                //it's the top of an archive hierarchy
                DirectoryInfo di = new DirectoryInfo(StagedDataUri.LocalPath);
                FileInfo [] fi = di.GetFiles("*.*", SearchOption.AllDirectories);
                FileInfo [] fi2 = FileFilterOptions.FilterFiles(fi);
                foreach (FileInfo f in fi2)
                    Result.Add(new Uri(f.FullName));
            }
            return Result.ToArray();
        }

        #endregion data staging


        #region geospatial methods

        /// <summary>
        /// Load a shapefile and return a dictionary of area key and centroid coordinates. This can be used to work
        /// out distances between any two areas.
        /// </summary>
        /// <param name="ShpFilename"></param>
        /// <returns></returns>
        public Dictionary<string, NetTopologySuite.Geometries.Point> LoadCentroidsFromShapefile(string ShpFilename)
        {
            System.Diagnostics.Debug.WriteLine("LoadCentroidsFromShapefile");
            System.Diagnostics.Debug.WriteLine(Directory.GetCurrentDirectory());
            //C:\richard\stingray\talisman\trunk\DatastoreMiner\DatastoreMiner\bin\Debug
            //build a hash of area key and centroid that we can use to calculate every possible distance from
            //also added hash of area key and polygon area for intrazone distance calculation
            Dictionary<string, NetTopologySuite.Geometries.Point> centroids = new Dictionary<string, NetTopologySuite.Geometries.Point>();
            //Dictionary<string, double> areas = new Dictionary<string, double>();
            //load the shapefile
            ShapeFile sf = new ShapeFile(ShpFilename);
            sf.Open();
            var numFeatures = sf.GetFeatureCount();
            for (uint i = 0; i < numFeatures; i++)
            {
                var fdr = sf.GetFeature(i);
                var g = fdr.Geometry;
                var length = g.Length;
                NetTopologySuite.Geometries.Point centroid = (NetTopologySuite.Geometries.Point)g.Centroid;
                //now need the area key code
                string AreaKey = fdr.ItemArray[1] as string; //there are only three columns in the shapefile, the_geom (=0), code (=1) and plain text name (=2)
                centroids.Add(AreaKey, centroid); //assumes area keys are unique - they needn't be
                //and add the (complex) polygon area
                //areas.Add(AreaKey, g.Area);
            }
            sf.Close();

            System.Diagnostics.Debug.WriteLine("Finished LoadCentroidsFromShapefile");
            return centroids;
        }

        #endregion geospatial methods

        /// <summary>
        /// Read the output log file generated by ProcessSpatialCorrelate and return the last file and column indices for which
        /// there is correlation data. This allows continuation from the point that the process left off in the event of the
        /// program being forcibly stopped.
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns>A hashset of existing values keyed on "FileI_FileJ_VarI_VarJ" i.e. a unique string</returns>
        private HashSet<string> ParseContinuationSpatialCorrelate(string Filename)
        {
            HashSet<string> Existing = new HashSet<string>();
            if (File.Exists(Filename))
            {
                using (StreamReader reader = File.OpenText(Filename))
                {
                    string Line;
                    while ((Line = reader.ReadLine()) != null)
                    {
                        string[] Fields = Line.Split(new char[] { ',' }); //there aren't any quoted fields
                        if (Fields.Length > 6)
                        {
                            //this is fields FileI_FileJ_VarI_VarJ
                            string Key = Fields[3] + "_" + Fields[4] + "_" + Fields[5] + "_" + Fields[6];
                            Existing.Add(Key);
                        }
                    }
                }
            }
            return Existing;
        }

        /// <summary>
        /// Correlate all the files in a datastore to find spatial similarities
        /// TODO: need a weights file (geography) and a correlation function
        /// </summary>
        public void ProcessSpatialCorrelate()
        {
            //load centroids from MSOA shapefile as we need these for the spatial weights
            Dictionary<string, NetTopologySuite.Geometries.Point> Centroids = LoadCentroidsFromShapefile(MSOAShapefile);

            //build a hash of existing records in the log file
            HashSet<string> Existing = ParseContinuationSpatialCorrelate(Path.Combine(ImageDirectory, ReportFile));
            //TextWriter writer = File.CreateText(Path.Combine(ImageDirectory, ReportFile));
            TextWriter writer = File.AppendText(Path.Combine(ImageDirectory, ReportFile));           

            //make sure the data staging area exists
            Directory.CreateDirectory(DataStagingDir);

            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
            int UniqueKeyColIdx = Catalogue.Columns.IndexOf(Schema.UniqueKeyField);

            int N = Catalogue.Rows.Count;
            int count = 0;
            int numfiles = (int)((float)((N + 1)*N) / 2.0f); //(N+1)*N/2 ... arithmetic progression
            Stopwatch timer;

            //NOTE: this isn't strictly the same as the image match version as it doesn't correlate columns within the
            //same file, only every column in file 1 against every column in file 2 for every file combination
            //You could fix this by doing the self correlation first in the i loop, then moving on to other files

            timer = Stopwatch.StartNew();
            for (int i = 0; i < Catalogue.Rows.Count; i++)
            {
                DataRow Row_i = Catalogue.Rows[i];
                string Title_i = Row_i[TitleColIdx] as string;
                string DataLink_i = Row_i[LinkColIdx] as string;
                string UniqueKey_i = Row_i[UniqueKeyColIdx] as string;

                if (string.IsNullOrEmpty(DataLink_i)) continue; //no data so skip

                //Data staging - download to the local file system and unzip if necessary
                Uri StagedDataUri_i = StageData(new Uri(DataLink_i)); //this is either the root of the extracted zip hierarchy, or an actual file
                Uri[] StagedDataFiles_i = FilterDataFiles(StagedDataUri_i); //get a list of files under the staging area that might contain data

                for (int j = i; j < Catalogue.Rows.Count; j++) //why start from i rather than i+1? auto-spatial correlation
                {
                    ++count;
                    float pct = ((float)count) / ((float)numfiles) * 100.0f;
                    System.Diagnostics.Debug.WriteLine("i=" + i + " j=" + j + " /" + count + "(" + pct + "%) " + DateTime.Now);

                    DataRow Row_j = Catalogue.Rows[j];
                    string Title_j = Row_j[TitleColIdx] as string;
                    string DataLink_j = Row_j[LinkColIdx] as string;
                    string UniqueKey_j = Row_j[UniqueKeyColIdx] as string;

                    if (string.IsNullOrEmpty(DataLink_j)) continue; //no data so skip

                    //Data staging - download to the local file system and unzip if necessary
                    Uri StagedDataUri_j = StageData(new Uri(DataLink_j)); //this is either the root of the extracted zip hierarchy, or an actual file
                    Uri[] StagedDataFiles_j = FilterDataFiles(StagedDataUri_j); //get a list of files under the staging area that might contain data

                    foreach (Uri FileUri_i in StagedDataFiles_i)
                    {
                        //slightly convoluted way of getting the column types for each csv column, but the geometry finder
                        //was designed to do this and check the geometry in the process
                        //BUT - we do need to know the geometry column so that we can match the data from the two files
                        //even if we know it's all going to be OA data

                        //parse column types for i file
                        //MapTube.GIS.DataLoader loader_i = new MapTube.GIS.DataLoader();
                        //loader_i.LoadHeaderLine(FileUri_i.ToString(), out Headers_i);
                        GeometryFinder GF_i = new GeometryFinder(ConfigFilename);
                        GF_i.ProbablePointGeometryFromDataFile(FileUri_i); //trigger parsing of column types
                        List<ColumnType> Cols_i = GF_i.ProbablePointDataColumnTypes;

                        foreach (ColumnType Col_i in Cols_i)
                        {
                            if (!Col_i.IsNumeric) continue;
                            //load i column of data with keys here
                            //TODO: area key and data key here - hardcoded area key as first column for now
                            Dictionary<string, float> data_i = LoadDataColumn(FileUri_i,Cols_i[0].Name,Col_i.Name);

                            foreach (Uri FileUri_j in StagedDataFiles_j)
                            {
                                //parse column types for j file
                                GeometryFinder GF_j = new GeometryFinder(ConfigFilename);
                                GF_j.ProbablePointGeometryFromDataFile(FileUri_j); //trigger parsing of column types
                                List<ColumnType> Cols_j = GF_j.ProbablePointDataColumnTypes;

                                foreach (ColumnType Col_j in Cols_j)
                                {
                                    //check whether this FileI, FileJ, ColI, ColJ pattern already exists in the log file and skip if it does
                                    string Key = string.Format("{0}_{1}_{2}_{3}", i, j, Col_i.Index, Col_j.Index);
                                    if (Existing.Contains(Key)) {
                                        Console.Out.WriteLine("Skipping: " + Key);
                                        continue;
                                        //NOTE: you don't need to add new keys to Existing as the order is sequential
                                    }
                                    if (!Col_j.IsNumeric) continue;

                                    //load j column of data with keys here
                                    //todo: area key and data key here - hardcoded area key as first column for now
                                    Dictionary<string, float> data_j = LoadDataColumn(FileUri_j, Cols_j[0].Name, Col_j.Name);

                                    //now put data_i and data_j together by joining on the area key
                                    List<double> X = new List<double>();
                                    List<double> Y = new List<double>();
                                    List<NetTopologySuite.Geometries.Point> C = new List<NetTopologySuite.Geometries.Point>();

                                    foreach (KeyValuePair<string,float> KVP in data_i)
                                    {
                                        if (data_j.ContainsKey(KVP.Key))
                                        {
                                            X.Add((double)KVP.Value);
                                            Y.Add((double)data_j[KVP.Key]);
                                            C.Add(Centroids[KVP.Key]); //correct centroid point for this area
                                        }
                                    }
                                    //now make the centroids array
                                    double[,] Cxy = new double[C.Count,2];
                                    for (int k = 0; k < C.Count; k++)
                                    {
                                        Cxy[k,0] = C[k].X;
                                        Cxy[k,1] = C[k].Y;
                                    }


                                    //and after all that, we finally get to correlate two vectors
                                    double[] I = Correlation.SpatialBivariateMoranI(X.ToArray(), Y.ToArray(), Cxy);

                                    //write results
                                    Tee(Console.Out, writer,
                                        String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}\n",
                                        "Correlate", I[0], I[1], i, j, Col_i.Index, Col_j.Index,
                                        Path.GetFileName(FileUri_i.LocalPath), Path.GetFileName(FileUri_j.LocalPath),
                                        Col_i.Name, Col_j.Name, X.Count, Y.Count, C.Count,
                                        timer.ElapsedMilliseconds));
                                    writer.Flush();
                                    
                                }
                            }
                        }
                    }

                }
            }
            
        }

        /// <summary>
        /// Fast version of ProcessSpatialCorrelate using the load all data into a table first method for speed (like ProcessKNearestNeighbourCorrelate).
        /// </summary>
        public void ProcessSpatialCorrelateFast()
        {
            //NOTE: most of this was taken from the KNN code and I'm being a bit paranoid about making sure the X,Y and centroid areas really do match up, which is good.
            //load centroids from MSOA shapefile as we need these for the spatial weights
            Dictionary<string, NetTopologySuite.Geometries.Point> Centroids = LoadCentroidsFromShapefile(MSOAShapefile);
            //now make a lookup between areas and position in array based on the shapefile ordering - this is the ordering of the areas in the matrix (see the build procedure)
            int i = 0;
            Dictionary<string, int> areas = new Dictionary<string, int>(); //needed for the matrix build - this is the area ordering in the matrix
            string[] A = new string[Centroids.Count]; //unfortunately, I need this as well for the KNN correlate
            foreach (string AreaKey in Centroids.Keys)
            {
                areas.Add(AreaKey, i);
                A[i] = AreaKey;
                ++i;
            }
            //Spatial correlate uses the centroid as an array of [x,y] doubles, so we need to make this.
            //Go through centroids and lookup the area key in the areas dictionary that we just made (NOTE: it should all line up anyway, but let's make absolutely sure).
            double[,] Cxy = new double[Centroids.Count, 2];
            foreach (KeyValuePair<string, NetTopologySuite.Geometries.Point> KVP in Centroids)
            {
                int ci = areas[KVP.Key];
                Cxy[ci, 0] = KVP.Value.X;
                Cxy[ci, 1] = KVP.Value.Y;
            }
            Correlation correlator = new Correlation(Cxy); //difference here with Knn as I rely on the array ordering of X,Y and Cxy for speed - Knn uses an area key lookup


            //now load ALL the data into one big table in memory!
            List<double[]> Matrix;
            List<string> VariableNamesIndex;
            BinaryFormatter formatter = new BinaryFormatter();

            //load existing (for speed)
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "matrix.bin"), FileMode.Open))
            {
                Matrix = (List<double[]>)formatter.Deserialize(reader);
            }
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "varnamesindex.bin"), FileMode.Open))
            {
                VariableNamesIndex = (List<string>)formatter.Deserialize(reader);
            }

            //OK, that should have taken a while, now we can do the correlation
            Stopwatch timer = Stopwatch.StartNew();
            TextWriter teewriter = File.AppendText(Path.Combine(ImageDirectory, Path.Combine(ImageDirectory, "processspatialcorrelatefast.csv")));
            Tee(Console.Out, teewriter, "I,i,j,VarName_i,VarName_j,milliseconds\n");
            //System.Diagnostics.Debug.WriteLine("I,i,j,VarName_i,VarName_j,milliseconds");
            int N = VariableNamesIndex.Count;
//HACK! note 60 here!!!!
            for (i = 60; i < N; i++)
            {
                string VarName_i = VariableNamesIndex[i];
                double[] X = Matrix[i];
                for (int j = i; j < N; j++)
                {
                    string VarName_j = VariableNamesIndex[j];
                    double[] Y = Matrix[j];
                    //double[] I = Correlation.SpatialBivariateMoranI(X.ToArray(), Y.ToArray(), Cxy);
                    double I = correlator.SpatialBivariateMoranIFast(X.ToArray(), Y.ToArray());
                    //NOTE: I've dumped the I[0] factor here as it was useless
                    Tee(Console.Out, teewriter, I + "," + i + "," + j + "," + VarName_i + "," + VarName_j + "," + timer.ElapsedMilliseconds + "\n");
                    //System.Diagnostics.Debug.WriteLine(I + "," + i + "," + j + "," + VarName_i + "," + VarName_j+","+timer.ElapsedMilliseconds);
                    teewriter.Flush();
                }
            }

        }

        /// <summary>
        /// K Nearest neighbour correlation using the load all data into a table first method for speed.
        /// </summary>
        /// <param name="K"></param>
        public void ProcessKNearestNeighbourCorrelate(int K)
        {
            //load centroids from MSOA shapefile as we need these for the spatial weights
            Dictionary<string, NetTopologySuite.Geometries.Point> Centroids = LoadCentroidsFromShapefile(MSOAShapefile);
            KNearestNeighbour KNN = new KNearestNeighbour(K, Centroids);
            //now make a lookup between areas and position in array based on the shapefile ordering
            int i = 0;
            Dictionary<string, int> areas = new Dictionary<string, int>(); //needed for the matrix build
            string[] A = new string[Centroids.Count]; //unfortunately, I need this as well for the KNN correlate
            foreach (string AreaKey in Centroids.Keys)
            {
                areas.Add(AreaKey, i);
                A[i] = AreaKey;
                ++i;
            }
            
            //now load ALL the data into one big table in memory!
            List<double[]> Matrix;
            List<string> VariableNamesIndex;
            BinaryFormatter formatter = new BinaryFormatter();
            //create and save
            //BuildDataMatrix(areas, out Matrix, out VariableNamesIndex);
            //using (FileStream writer = new FileStream(Path.Combine(ImageDirectory, "matrix.bin"), FileMode.Create))
            //{
            //    formatter.Serialize(writer, Matrix);
            //}
            //using (FileStream writer = new FileStream(Path.Combine(ImageDirectory, "varnamesindex.bin"), FileMode.Create))
            //{
            //    formatter.Serialize(writer, VariableNamesIndex);
            //}
            //load existing (for speed)
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "matrix.bin"), FileMode.Open))
            {
                Matrix = (List<double[]>)formatter.Deserialize(reader);
            }
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "varnamesindex.bin"), FileMode.Open))
            {
                VariableNamesIndex = (List<string>)formatter.Deserialize(reader);
            }
            
            //OK, that should have taken a while, now we can do the correlation
            Stopwatch timer = Stopwatch.StartNew();
            TextWriter teewriter = File.AppendText(Path.Combine(ImageDirectory, Path.Combine(ImageDirectory,"processknearestneighbourcorrelate.csv")));
            Tee(Console.Out,teewriter,"I,i,j,VarName_i,VarName_j,milliseconds");
            //System.Diagnostics.Debug.WriteLine("I,i,j,VarName_i,VarName_j,milliseconds");
            int N = VariableNamesIndex.Count;
            for (i = 0; i < N; i++)
            {
                string VarName_i = VariableNamesIndex[i];
                double[] X = Matrix[i];
                for (int j = i; j < N; j++)
                {
                    string VarName_j = VariableNamesIndex[j];
                    double[] Y = Matrix[j];
                    double I = KNN.Correlate(A, X, Y);
                    Tee(Console.Out,teewriter,I + "," + i + "," + j + "," + VarName_i + "," + VarName_j+","+timer.ElapsedMilliseconds);
                    //System.Diagnostics.Debug.WriteLine(I + "," + i + "," + j + "," + VarName_i + "," + VarName_j+","+timer.ElapsedMilliseconds);
                }
            }
        }

        /// <summary>
        /// Load EVERYTHING into a matrix so it's ALL in memory and easier to work with
        /// </summary>
        /// <param name="areas">Pass in a list of area names to zonei index</param>
        /// <param name="Matrix">Actually, a list of dataset columns where the float[] data matches the areas passed in</param>
        /// <param name="VariableNamesIndex">Lookup between the name of a variable and its List index in Matrix</param>
        public void BuildDataMatrix(Dictionary<string, int> areas, out List<double[]> Matrix, out List<String> VariableNamesIndex)
        {
            Stopwatch timer = Stopwatch.StartNew();
            Matrix = new List<double[]>();
            VariableNamesIndex = new List<string>(); //list index follows matrix list index

            //make sure the data staging area exists
            Directory.CreateDirectory(DataStagingDir);

            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
            int UniqueKeyColIdx = Catalogue.Columns.IndexOf(Schema.UniqueKeyField);

            int Var_i = 0; //column in Matrix where we're going to put the data i.e. the List index
            //int N = Catalogue.Rows.Count;
            for (int i = 0; i < Catalogue.Rows.Count; i++)
            {
                DataRow Row_i = Catalogue.Rows[i];
                string Title_i = Row_i[TitleColIdx] as string; //plain text name
                string DataLink_i = Row_i[LinkColIdx] as string; //uri
                string UniqueKey_i = Row_i[UniqueKeyColIdx] as string;

                if (string.IsNullOrEmpty(DataLink_i)) continue; //no data so skip

                //Data staging - download to the local file system and unzip if necessary
                Uri StagedDataUri_i = StageData(new Uri(DataLink_i)); //this is either the root of the extracted zip hierarchy, or an actual file
                Uri[] StagedDataFiles_i = FilterDataFiles(StagedDataUri_i); //get a list of files under the staging area that might contain data
                foreach (Uri FileUri_i in StagedDataFiles_i)
                {
                    System.Diagnostics.Debug.WriteLine("Loading: " + FileUri_i);

                    GeometryFinder GF_i = new GeometryFinder(ConfigFilename);
                    GF_i.ProbablePointGeometryFromDataFile(FileUri_i); //trigger parsing of column types
                    List<ColumnType> Cols_i = GF_i.ProbablePointDataColumnTypes;
                    foreach (ColumnType Col_i in Cols_i)
                    {
                        if (!Col_i.IsNumeric) continue;
                        //load i column of data with keys here
                        //TODO: area key and data key here - hardcoded area key as first column for now
                        Dictionary<string, float> data_i = LoadDataColumn(FileUri_i, Cols_i[0].Name, Col_i.Name);
                        double[] data = new double[areas.Count];
                        foreach (KeyValuePair<string, float> KVP in data_i)
                        {
                            int zonei = areas[KVP.Key]; //could fail...?
                            data[zonei] = (double)KVP.Value; //put data in correct place
                        }
                        Matrix.Add(data);
                        VariableNamesIndex.Add(Col_i.Name); //name of this column and where he is
                        ++Var_i;
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("BuildDataMatrix: " + timer.ElapsedMilliseconds + " ms");
        }


        /// <summary>
        /// Create a correlation from the PDF description files for tables.
        /// Most of this is deferred to the keyword processor.
        /// </summary>
        public void ProcessKeywordCorrelate()
        {
            KeywordProcessor kp = new KeywordProcessor(Path.Combine(DataRootDir,StopWordsFilename));
            kp.GenerateTextCorrelation(Catalogue, Schema);
        }

        /// <summary>
        /// Load a column of data from a csv file along with the area key codes
        /// </summary>
        /// <param name="URIFilename">File to load, either local or remote</param>
        /// <param name="AreaKeyField">The name of the field in the csv file containing the area key column</param>
        /// <param name="DataField">The name of the field in the csv file containing the data column</param>
        /// <returns></returns>
        public Dictionary<string, float> LoadDataColumn(Uri URIFilename, string AreaKeyField, string DataField)
        {
            Dictionary<string, float> Data = new Dictionary<string, float>();

            //string [] Headers = null;
            //MapTube.GIS.DataLoader loader = new MapTube.GIS.DataLoader();
            //loader.LoadHeaderLine(URIFilename.ToString(),out Headers);

            //use the more basic MapTubeD data loader which takes a delegate
            int AreaKeyIndex = -1, DataFieldIndex = -1;
            DataLoader.DataLineReadDelegate reader = delegate(string[] Headers, string[] line)
            {
                //first time around use the headers to get the indexes for the area key and data field columns
                if (AreaKeyIndex <= 0)
                {
                    for (int i=0; i<Headers.Length; i++)
                    {
                        string name = Headers[i];
                        if (name == AreaKeyField) AreaKeyIndex = i;
                        if (name == DataField) DataFieldIndex = i;
                    }
                }
                //the actual data bit
                //TODO: trap malformed lines, bad numeric values and duplicate area keys?
                string AreaKey = line[AreaKeyIndex];
                float Value = Convert.ToSingle(line[DataFieldIndex]);
                Data.Add(AreaKey, Value);
            };

            DataLoader loader = new DataLoader();
            loader.ReadCSV(URIFilename,reader);

            return Data;
        }

        /// <summary>
        /// Build thumbnail images of all the maps where we understand the data
        /// </summary>
        public void Process()
        {
            GeometryFinder GF = new GeometryFinder(ConfigFilename);

            TextWriter writer = File.CreateText(Path.Combine(ImageDirectory,ReportFile));
            TextWriter index_writer = File.CreateText(Path.Combine(ImageDirectory, MapIndexFile));
            //csv index file header line: major_index is dataset number, minor_index is column in dataset, data_uri is location of data (local staging),
            //unique key is a key that links back to the catalogue for this dataset, title is the plain text name from the catalogue and column is the
            //name of the column being mapped
            index_writer.WriteLine("major_index,minor_index,data_uri,uniquekey,title,column");

            //make sure the data staging area exists
            Directory.CreateDirectory(DataStagingDir);

            try
            {
                //now read the data
                _AreaGeometryCount = 0; //number of datasets recognised as area geometry
                _PointGeometryCount = 0; //number of datasets recognised as point geometry
                int count = 0; //number of datasets processed

                int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
                int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
                int UniqueKeyColIdx = Catalogue.Columns.IndexOf(Schema.UniqueKeyField);

                foreach (DataRow Row in Catalogue.Rows)
                {
                    string Title = Row[TitleColIdx] as string;
                    string DataLink = Row[LinkColIdx] as string;
                    string UniqueKey = Row[UniqueKeyColIdx] as string;

                    if (!string.IsNullOrEmpty(DataLink))
                    {
                        //Data staging - download to the local file system and unzip if necessary
                        Uri StagedDataUri = StageData(new Uri(DataLink)); //this is either the root of the extracted zip hierarchy, or an actual file
                        Uri[] StagedDataFiles = FilterDataFiles(StagedDataUri); //get a list of files under the staging area that might contain data

                        float percent = (float)count / (float)DatasetCount * 100.0f;
                        Tee(Console.Out, writer, count + " / " + DatasetCount + "(" + percent + "%)\n");
                        Tee(Console.Out, writer, "Title: " + Title + "\n");
                        Tee(Console.Out, writer, "Data Link: " + DataLink + "\n");

                        //now get the files and analyse it
                        foreach (Uri FileUri in StagedDataFiles)
                        {
                            //we should have a true file (not dir) at this point and it should be a valid type as it's been filtered (*.csv)
                            if (FileUri.LocalPath.ToLower().EndsWith(".csv"))
                            {
                                Tee(Console.Out, writer, "Staged File: " + FileUri.LocalPath + "\n");
                                writer.Flush(); //next bit is computationally intensive, so flush report file now so it's up to date if you about during the next bit
                                ProcessFile(FileUri, GF, writer, index_writer, UniqueKey, Title, count);
                            }
                        }
                    }
                    ++count;
                    Tee(Console.Out, writer, "=======================================================================\n");
                }

                Tee(Console.Out, writer, "Dataset Count=" + DatasetCount + "\n");
                Tee(Console.Out, writer, "CSV Link Count=" + CSVLinkCount + "\n");
                Tee(Console.Out, writer, "Area Geometry Count=" + _AreaGeometryCount + "\n");
                Tee(Console.Out, writer, "Point Geometry Count=" + _PointGeometryCount + "\n");
            }
            finally
            {
                writer.Close();
                index_writer.Close();
            }
        }

        /// <summary>
        /// Process a single CSV data file into a thumbnail map
        /// </summary>
        /// <param name="CSVUri">Location of data (local staging)</param>
        /// <param name="GF">MapTubeD object for geocoding</param>
        /// <param name="writer">General log describing what's going on and coding scores for datasets</param>
        /// <param name="index_writer">Writes out a csv file that describes what each one of the images represents</param>
        /// <param name="UniqueKey">Code that uniquely identifies this dataset as a line in the catalogue</param>
        /// <param name="Title">Plain text title of theis dataset from the catalogue</param>
        /// <param name="count">Counter for major index number that counts mapped datasets i.e. 0,1,2,3...</param>
        private void ProcessFile(Uri CSVUri, GeometryFinder GF, TextWriter writer, TextWriter index_writer, string UniqueKey, string Title, int count)
        {
            //todo: could do a check file exists here?
            
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
                    ++_AreaGeometryCount;
                    //This is just the winning area geometry
                    //Tee(Console.Out, writer, "Area Geometry: " + GeometryName + " Column=" + ColumnIndex + " probability=" + prob + "\n");
                    //This is all the geometries that made the threshold
                    var sorted_probs = probs.OrderByDescending(p => p.Value); //sort matched columns by probability values
                    if ((sorted_probs.Count() > 0) && ((sorted_probs.ElementAt(0).Value - sorted_probs.ElementAt(1).Value) <= float.Epsilon))
                    {
                        Tee(Console.Out, writer, "Area Geometry tie. Using _GeomHints\n");

                        //We have a tie, so use the geometry hints for additional weighting.
                        //Need to create a new GeometryProbabilites object
                        GeometryProbabilities weighted_probs = new GeometryProbabilities();
                        foreach (KeyValuePair<GeometryColumn,float> KVP in probs)
                        {
                            GeometryColumn col = KVP.Key;
                            if (_GeomHints.ContainsKey(col.GeometryName))
                                col.Probability*= _GeomHints[col.GeometryName];
                            weighted_probs.Add(col.GeometryName, col.ColumnIndex, col.ColumnName, col.Probability);
                        }
                        //now switch old probs and new weighted ones
                        probs = weighted_probs;
                        //and then do a re-sort
                        sorted_probs = probs.OrderByDescending(p => p.Value);
                    }
                    foreach (KeyValuePair<GeometryColumn, float> p in sorted_probs)
                    {
                        Tee(Console.Out, writer, "Area Geometry: " + p.Key.GeometryName + " Column=" + p.Key.ColumnIndex + " probability=" + p.Key.Probability + "\n");
                    }
                }
                
                //Second, try a point geometry
                PointGeometryXYCRS PXYCRS = GF.ProbablePointGeometryFromDataFile(CSVUri);
                if (!string.IsNullOrEmpty(PXYCRS.XFieldName))
                {
                    ++_PointGeometryCount;
                    Tee(Console.Out, writer, "Point Geometry: XField=" + PXYCRS.XFieldName + " YField=" + PXYCRS.YFieldName + "\n");
                    //write out CRS?
                }
                //write out the column type information gained from the point data analysis
                foreach (ColumnType col in GF.ProbablePointDataColumnTypes)
                {
                    Tee(Console.Out, writer, col.Name);
                    if (col.IsIndex)
                    {
                        Tee(Console.Out, writer, " (Index)");
                    }
                    else if (col.IsNumeric)
                    {
                        Tee(Console.Out, writer, " Numeric Min=" + col.Min + " Max=" + col.Max);
                    }
                    Tee(Console.Out, writer, "\n");
                }
                
                //Make preview maps...
                Tee(Console.Out, writer, "Making map...\n");
                Tee(Console.Out, writer, "Map image id=" + count+"\n");
                //false below means don't draw the title text on the image - required for image classification
                writer.Flush(); //next bit is computationally intensive, so flush report file now in case you abort on the next bit
                MakeMap(CSVUri, writer, index_writer, false, UniqueKey, Title, Convert.ToString(count), GF, probs);
            }
            catch (Exception ex)
            {
                Tee(Console.Out, writer, "Exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Write a string out to two distinct streams (writers)
        /// </summary>
        /// <param name="out1"></param>
        /// <param name="out2"></param>
        /// <param name="line"></param>
        protected static void Tee(TextWriter out1, TextWriter out2, string line)
        {
            //tee output to two streams
            if (out1 != null) out1.WriteLine(line);
            if (out2 != null) out2.WriteLine(line);
            //nasty hack to get around newline problems
            //if (line.EndsWith("\n")) out2.WriteLine();
        }

        /// <summary>
        /// Make maps of all possible fields in a URI
        /// </summary>
        /// <param name="CSVUri">URI of the data</param>
        /// <param name="writer">TextWriter to write out information to i.e. log file</param>
        /// <param name="index_writer">Writes out a csv file that describes what each one of the images represents</param>
        /// <param name="TextOnImage">If true, then draw the title text on the image, otherwise image contains no text</param>
        /// <param name="UniqueKey">Code that uniquely identifies this dataset as a line in the catalogue</param>
        /// <param name="Title">Plain text title of this map from the mainfest</param>
        /// <param name="UniqueId">Unique Id identifying this data in the datastore. The unique filename is generated
        /// from the id and a counter for the field number in the data. Uses the london datastore number.</param>
        /// <param name="GF"></param>
        /// <param name="probs"></param>
        protected void MakeMap(Uri CSVUri, TextWriter writer, TextWriter index_writer, bool TextOnImage, string UniqueKey, string Title, string UniqueId, GeometryFinder GF, GeometryProbabilities probs)
        {
            //if images have already been built for this dataset, then skip to the next one
            string pattern = "image_"+UniqueId+"_*.png";
            string[] files = Directory.GetFiles(ImageDirectory,pattern);
            if (files.Length > 0) return;

            Font font = new Font("Arial", 8, FontStyle.Bold);
            Brush brush = Brushes.Black;

            CoordinateSystemFactory CSFactory = new CoordinateSystemFactory();
            ICoordinateSystem CSMerc = CSFactory.CreateFromWkt(MapTubeD.Projections.GoogleProj);
            ICoordinateSystem CSWGS84 = CSFactory.CreateFromWkt(MapTubeD.Projections.WGS84Proj);
            CoordinateTransformationFactory ctf = new CoordinateTransformationFactory();
            ICoordinateTransformation trans = ctf.CreateFromCoordinateSystems(CSWGS84, CSMerc);

            int Id = 0;
            foreach (KeyValuePair<GeometryColumn, float> KVP in probs)
            {
                if (KVP.Value >= 0.8)
                {
                    MapTubeD.DataCache.MapDataDescriptor desc = new MapTubeD.DataCache.MapDataDescriptor();
                    MapTubeD.DBJoin dbjoin = new MapTubeD.DBJoin();
                    desc.Data = dbjoin;
                    dbjoin.AreaKey = GF.ProbablePointDataColumnTypes[KVP.Key.ColumnIndex].Name;
                    dbjoin.Geometry = KVP.Key.GeometryName;
                    foreach (ColumnType ctype in GF.ProbablePointDataColumnTypes)
                    {
                        if (ctype.IsNumeric)
                        {
                            //make a map from it!
                            //NOTE: MapTubeD expects to load the descriptor from the web. We can either upload a
                            //descriptor file from here, or create one manually by loading the data from the uri.
                            //NOTE2: I can't believe DBJoin exposes the load method!
                            //NOTE3: instead of manipulating the descriptor classes manually, you could use the
                            //descriptor file types and serialize
                            string DataField = ctype.Name;
                            dbjoin.LoadCSVData(CSVUri.ToString(), DataField);
                            //OK, now we need a style...
                            //the rest of this is 'borrowed' from the webmapcreator
                            string[] Headers;
                            MapTube.GIS.DataLoader loader = new MapTube.GIS.DataLoader();
                            //TODO: Geometry finder doesn't need to successively load the data vvvvvvvv (YES! DataField)
                            MapTube.GIS.DataLoader.StatusCode Success = loader.LoadCSVData(CSVUri.ToString(), DataField, out Headers);
                            if (Success != MapTube.GIS.DataLoader.StatusCode.FAIL)
                            {
                                MapTubeD.ColourScale cs = new MapTubeD.ColourScale();
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
                                    Color[] cols = MapTube.GIS.Colours.FindNamedColours(SequentialScheme, breaksvalues.Length);
                                    for (int i = 0; i < breaksvalues.Length; i++)
                                    {
                                        string text = cs.getThresholdTextByIndex(i);
                                        cs.addColour(cols[i], breaksvalues[i], text);
                                    }
                                }
                                desc.MapColourScale = cs;
                                desc.MapRenderStyle = new MapTubeD.RenderStyle();
                                //OK, having gone to all this trouble, we really need to draw something
                                MapTubeD.TileRenderers.DBJoinTileRenderer tr = new MapTubeD.TileRenderers.DBJoinTileRenderer(ConfigFilename);
                                //this is rather unfortunate, need WGS84 AND Merc
                                RectangleF WGS84 = tr.GetDataBoundsWGS84(desc);
                                Envelope WGS84Env = new Envelope(WGS84.Left, WGS84.Right, WGS84.Bottom, WGS84.Top);
                                Envelope MercEnv = (Envelope)GeometryTransform.TransformBox(WGS84Env, trans.MathTransform);
                                //now fix aspect ratio of Mercator evnelope to be square - WGS84 extents not actuall used
                                double CX = (MercEnv.MinX + MercEnv.MaxX) / 2;
                                double CY = (MercEnv.MinY + MercEnv.MaxY) / 2;
                                double size = Math.Max(MercEnv.Width, MercEnv.Height);
                                MercEnv = new Envelope(CX - size / 2, CX + size / 2, CY - size / 2, CY + size / 2);
                                Bitmap image = new Bitmap(256, 256); //what size should we use? - ASPECT? - ONLY 256 WORKS!
                                using (Graphics g = Graphics.FromImage(image))
                                {
                                    g.FillRectangle(Brushes.LightGray, 0, 0, 256, 256);
                                }
                                if (tr.DrawTile(desc, image, WGS84Env, MercEnv))
                                {
                                    if (TextOnImage)
                                    {
                                        //draw title and datafield number onto image if required
                                        using (Graphics g = Graphics.FromImage(image))
                                        {
                                            g.DrawString(Title, font, brush, new RectangleF(0, 0, 256, 64));
                                            g.DrawString(DataField, font, brush, 0, 32);
                                        }
                                    }
                                    Tee(Console.Out, writer, "MapImage: image_" + UniqueId + "_" + Id + ".png Title: " + Title + " DataField:" + DataField + "\n");
                                    index_writer.WriteLine(UniqueId + "," + Id + ",\"" + CSVUri.ToString() + "\",\"" + UniqueKey + "\",\"" + Title + "\",\"" + DataField+"\""); //this writes out a csv file linking images back to the data
                                    index_writer.Flush();
                                    image.Save(Path.Combine(ImageDirectory,"image_" + UniqueId + "_" + Id + ".png"), ImageFormat.Png);
                                    ++Id;
                                }
                                else
                                    //Tee(Console.Out, writer, "Error making map: "+CSVUri+" "+DataField);
                                    System.Diagnostics.Debug.WriteLine("Error drawing tile: " + CSVUri + " " + DataField);
                            }

                        }
                    }
                }
            }
        }

        /// <summary>
        /// Take all the map thumbnails in the image directory created by MakeMap and create one large image
        /// containing everything ready to be processed using the ImageCutter.
        /// </summary>
        /// <param name="dir">Directory containing the images</param>
        public static void MakeBigImage(string dir)
        {
            //string dir = "..\\..\\Data\\images\\";
            //string dir = _ImageDirectory;

            DirectoryInfo di = new DirectoryInfo(dir);
            FileInfo[] imagefiles = di.GetFiles("*.png");
            //square image
            //int dimension = (int)Math.Ceiling(Math.Sqrt(imagefiles.Length));
            //NOTE: all A series paper i.e. A3, A4 etc has an aspect ratio of sqrt(2)!
            int dimensionx = 41, dimensiony = 62; //hacked for NOMIS data at 2:3 apect
            Bitmap BigImage = new Bitmap(dimensionx * 256, dimensiony * 256);
            using (Graphics g = Graphics.FromImage(BigImage))
            {
                g.FillRectangle(Brushes.LightGray, 0, 0, dimensionx, dimensiony);

                for (int i = 0; i < imagefiles.Length; i++)
                {
                    int x = (i % dimensionx) * 256;
                    int y = (i / dimensionx) * 256; //check this! (dimx is correct)
                    Bitmap SmallImage = new Bitmap(imagefiles[i].FullName);
                    g.DrawImageUnscaled(SmallImage, x, y);
                }
            }
            BigImage.Save("..\\..\\Data\\images\\BigLondonDatastore.png", ImageFormat.Png);
        }

        /// <summary>
        /// After building thumbnail images for all the maps, apply an image comparison process to generate a correlation matrix to see which are similar
        /// </summary>
        public void ImageMatch()
        {
            //CSV format is: i,j,imagei_major,imagei_minor,imagej_major,imagej_minor,imagei_filename,imagej_filename,value
            //not writing out header line as it's going to have to be sorted
            //image major and minor should be obvious from the filename, i and j are just the count of all image files in the directory
            using (TextWriter writer = File.CreateText(Path.Combine(ImageDirectory, ImageMatchReportFile)))
            {
                string pattern = "image*.png";
                string[] files = Directory.GetFiles(ImageDirectory, pattern);
                for (int i = 0; i < files.Length; i++)
                {
                    Image Imagei = Image.FromFile(files[i]);
                    for (int j = 0; j < files.Length; j++)
                    {
                        Image Imagej = Image.FromFile(files[j]);
                        double d = ImageMatcher.RGBDifference(Imagei, Imagej);
                        string[] fieldsi = Path.GetFileName(files[i]).Split(new char[] { '_', '.' }); //it's image_1_99.png, so get back the 1 and 99 in fields[1] and [2]
                        string[] fieldsj = Path.GetFileName(files[j]).Split(new char[] { '_', '.' });
                        Tee(Console.Out, writer, i + "," + j + "," + fieldsi[1]+","+fieldsi[2]+","+fieldsj[1]+","+fieldsj[2]+","+Path.GetFileName(files[i]) + "," + Path.GetFileName(files[j]) + "," + d+"\n");
                    }
                    writer.Flush();
                }

                writer.Close();
            }
        }

        /// <summary>
        /// Calculate probabilities for every pixel in every image using a histogram.
        /// </summary>
        public void PixelProbabilities()
        {
            string pattern = "image*.png";
            string[] files = Directory.GetFiles(ImageDirectory, pattern);
            for (int i = 0; i < files.Length; i++)
            {
                Image Imagei = Image.FromFile(files[i]);
                for (int y = 0; y < Imagei.Height; y++)
                {
                    for (int x = 0; x < Imagei.Width; x++)
                    {

                    }
                }
            }
        }


        #region Graphs

        /// <summary>
        /// Strip HTML tags out of a fragment of text. Used for Gov Datastore descriptions which begin and end with p /p
        /// TODO: this isn't going to be completely rigorous
        /// At the moment it doesn't understand CDATA sections
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string StripHtml(string Text)
        {
            StringBuilder builder = new StringBuilder();
            bool InTag = false;
            for (int i = 0; i < Text.Length; i++)
            {
                char ch = Text[i];
                if (ch == '<') InTag = true;
                else if (InTag)
                {
                    if (ch == '>') InTag = false;
                }
                else
                {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Turn a block of text into a list of words (lowercase with no punctuation).
        /// No words less than 3 characters are allowed.
        /// </summary>
        /// <param name="Text"></param>
        /// <returns></returns>
        public static string[] SplitWords(string Text)
        {
            //TODO: really need to process out http addresses before processing the text as English
            //split words on space, comma, dot or newline and process
            string[] Words = Text.Split(new char[] { ' ', ',', '.', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> NewWords = new List<string>();
            for (int i = 0; i < Words.Length; i++)
            {
                StringBuilder builder = new StringBuilder();
                for (int j = 0; j < Words[i].Length; j++)
                {
                    char ch = Words[i][j];
                    if ((ch >= 'a' && ch <= 'z') | (ch=='-')) //lowercase characters of hypen retained
                    {
                        builder.Append(ch);
                    }
                    else if (ch >= 'A' && ch <= 'Z') //uppercase characters converted to lower
                    {
                        builder.Append(Char.ToLower(ch));
                    }
                    //any other characters removed (numbers or punctuation)
                }
                //TODO: could remove leading and trailing non-alpha characters here (=- gets through on some of the words) 
                Words[i] = builder.ToString().Trim();
                if (!string.IsNullOrEmpty(Words[i])&&(Words[i].Length>2)) NewWords.Add(Words[i]);
            }
            return NewWords.ToArray();
        }

        /// <summary>
        /// Load the stop words list into a hash set
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns></returns>
        protected HashSet<string> LoadStopWords(string Filename)
        {
            HashSet<string> Result = new HashSet<string>();
            using (TextReader reader = File.OpenText(Filename))
            {
                string Line;
                while ((Line = reader.ReadLine()) != null)
                {
                    Result.Add(Line);
                }
            }
            return Result;
        }

        /// <summary>
        /// Build a graph where resource rows from the catalogue are linked where they share keywords.
        /// Each shared keyword between two vertices increases the weight between them by 1.0.
        /// No two vertices are connected by more than a single link.
        /// Pre-conditions: relies on this.TitleField and this.DescriptionField being set
        /// </summary>
        /// <returns>The graph</returns>
        public Graph<DataNode> MakeLinksGraph()
        {
            System.Diagnostics.Debug.WriteLine("MakeLinksGraph started");
            HashSet<string> StopWords = LoadStopWords(Path.Combine(DataRootDir,StopWordsFilename));
            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int DescriptionColIdx = Catalogue.Columns.IndexOf(Schema.DescriptionField);
            int TagsColIdx = Catalogue.Columns.IndexOf(Schema.TagsField);

            //Mapping between keyword and which resources use the keyword. Resource Id here is the line of the catalogue file.
            Dictionary<string, List<int>> KeywordToResourceId = new Dictionary<string, List<int>>();

            Graph<DataNode> G = new Graph<DataNode>(false);
            //now go through all the data and make a map of keywords used by resources
            float NumRows = (float)Catalogue.Rows.Count;
            System.Diagnostics.Debug.WriteLine("Number of resources in catalogue: " + NumRows);
            for (int ResourceId = 0; ResourceId < Catalogue.Rows.Count; ResourceId++)
            {
                DataRow Row = Catalogue.Rows[ResourceId];

                if (ResourceId%100==0)
                    System.Diagnostics.Debug.WriteLine("ResourceId: " + ResourceId + " "+(float)ResourceId/NumRows*100.0f+" %");
                string Title = Row[TitleColIdx] as string;
                string Description = Row[DescriptionColIdx] as string;
                string Tags = Row[TagsColIdx] as string;

                //if there is no description then we can't do anything with this resource
                //if (string.IsNullOrEmpty(Description)) continue;
                if (string.IsNullOrEmpty(Tags)) continue;

                //create the vertex for this line
                DataNode UserData = new DataNode();
                UserData.ResourceId = ResourceId;
                UserData.Title = Title;
                Vertex<DataNode> V = G.AddVertex(ResourceId, UserData);
                Title = Title.Replace("&", " ");
                V.VertexLabel = Title; //annoying, but there's no other way to set the vertex label

                //remove all punctuation, convert to lowercase and split words
                //System.Diagnostics.Debug.WriteLine("Description=" + Description);
                
                //code using Description to mine words
                //Description = StripHtml(Description);
                //string[] Words = SplitWords(Description);
                //code using Tags to mine words
                Tags = StripHtml(Tags); //is this necessary?
                string[] Words = SplitWords(Tags);
                
                //then add a resource id entry for every keyword
                foreach (string Word in Words)
                {
                    if (!StopWords.Contains(Word))
                    {
                        if (!KeywordToResourceId.ContainsKey(Word))
                            KeywordToResourceId.Add(Word, new List<int>()); //create word if new one
                        //add only if the word has not already been used with this resource id (i.e same word repeated in description)
                        //TODO: could strengthen the weight if word repeated, but Gephi seems to do this automatically where links are repeated (parallel edges?)
                        bool Found = false;
                        foreach (int ReId in KeywordToResourceId[Word]) //could sort the resource ids for better performance
                        {
                            if (ReId == ResourceId) { Found = true; break; }
                        }
                        if (!Found)
                        {
                            KeywordToResourceId[Word].Add(ResourceId); //add this resource to correct word entry
                            //System.Diagnostics.Debug.WriteLine("Word: " + Word + " Resource Id: " + ResourceId);
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("Keyword counts:");
            foreach (KeyValuePair<string, List<int>> KVP in KeywordToResourceId)
            {
                System.Diagnostics.Debug.WriteLine(KVP.Key + ": " + KVP.Value.Count);
            }

            System.Diagnostics.Debug.WriteLine("All resource lines processed, moving on to creating links");
            
            //This is the code to build the graph where each vertex is a resource and they are linked by common keywords.
            //As this results in 114 million links you really need to do one keyword per graph.
            //now all resource lines have been processed, add the links between the resource vertices
            float NumKeywords = (float)KeywordToResourceId.Count;
            int count = 0;
            System.Diagnostics.Debug.WriteLine("Number of keywords: " + NumKeywords);
            
            //build hashmap of all vertices containing the word "climate"
            //List<int> VertexList = KeywordToResourceId["climate"];
            //HashSet<int> VertexHash = new HashSet<int>();
            //foreach (int V in VertexList)
            //{
            //    System.Diagnostics.Debug.WriteLine("Climate Vertex Id: " + V + " " + Catalogue.Rows[V][TitleColIdx] as string);
            //    VertexHash.Add(V);
            //}

            foreach (KeyValuePair<string, List<int>> KVP in KeywordToResourceId)
            {
                //HACK!
                //if (count >= 5) break;
                //if (KVP.Key == "climate") continue; //much bigger, nastier hack! Don't create the fully connected climate connections
                if (KVP.Value.Count < 700) continue; //only take keywords with >900 edges
                
                System.Diagnostics.Debug.WriteLine("Number of edges: " + G.NumEdges);
                System.Diagnostics.Debug.WriteLine(KVP.Key + " : connecting " + KVP.Value.Count + " vertices sharing this keyword");
                if (count % 100 == 0)
                    System.Diagnostics.Debug.WriteLine("Keywords: " + count / NumKeywords * 100 + "%");
                //each word forms a fully connected group
                string Word = KVP.Key;
                List<int> ResourceIds = KVP.Value;
                //this is going to add n(n-1)/2 links
                //for (int i = 0; i < ResourceIds.Count; i++)
                //{
                //    for (int j = i + 1; j < ResourceIds.Count; j++)
                //    {
                //        G.ConnectVertices(ResourceIds[i], ResourceIds[j], Word, 1.0f);
                //        //System.Diagnostics.Debug.WriteLine("Keyword: "+Word+" Connecting " + ResourceIds[i] + " " + ResourceIds[j]);
                //    }
                //}
                //Modified code - only maintain a single link between nodes, but use the weight to count connections where they share keywords
                //TODO: should try to use single connect vertex function below
                for (int i = 0; i < ResourceIds.Count; i++)
                {
                    //if (!VertexHash.Contains(ResourceIds[i])) continue; //only connect vertices containing the "climate" keyword
                    Vertex<DataNode> Vi = G[ResourceIds[i]];
                    for (int j = i + 1; j < ResourceIds.Count; j++)
                    {
                        //if (!VertexHash.Contains(ResourceIds[j])) continue; //only connect vertices containing the "climate" keyword
                        //connect ResourceIds[i] and ResourceIds[j]
                    //    Vertex<DataNode> Vj = G[ResourceIds[j]];
                    //    bool Found = false;
                    //    foreach (Edge<DataNode> E in Vi.OutEdges)
                    //    {
                    //        if (E.ToVertex == Vj)
                    //        {
                    //            E.Weight += 1;
                    //            Found = true;
                    //            break;
                    //        }
                    //    }
                    //    if ((!Found) && (!G.IsDirected))
                    //    {
                    //        foreach (Edge<DataNode> E in Vi.InEdges)
                    //        {
                    //            if (E.FromVertex == Vj)
                    //            {
                    //                E.Weight += 1;
                    //                E.Label = E.Label + " " + Word;
                    //                Found = true;
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    if (!Found)
                    //    {
                    //        //need to make a new link
                    //        G.ConnectVertices(Vi, Vj, Word, 1.0f);
                    //    }
                        SingleConnectVertex(ref G, ResourceIds[i], ResourceIds[j]);
                    }
                }
                ++count;
            }

            //now go back and delete any orphan vertices
            for (int i = 0; i < G.Vertices.Count; i++)
            {
                Vertex<DataNode> V = G.Vertices[i];
                if ((V.OutEdges.Count == 0) && (V.InEdges.Count == 0))
                {
                    System.Diagnostics.Debug.WriteLine("Deleting vertex " + i);
                    G.DeleteVertex(V.VertexId);
                    --i;
                }
            }

            System.Diagnostics.Debug.WriteLine("MakeLinksGraph finished");

            return G;
        }

        /// <summary>
        /// Connect two vertices together, but if a link between them already exists, don't create a new one, but increase the weight
        /// </summary>
        /// <param name="G">The graph that we're operating on</param>
        /// <param name="i">The first vertex index</param>
        /// <param name="j">The second vertex index</param>
        protected void SingleConnectVertex(ref Graph<DataNode> G, int i, int j)
        {
            Vertex<DataNode> Vi = G[i];
            Vertex<DataNode> Vj = G[j];
            bool Found = false;
            foreach (Edge<DataNode> E in Vi.OutEdges)
            {
                if (E.ToVertex == Vj)
                {
                    E.Weight += 1;
                    Found = true;
                    break;
                }
            }
            if ((!Found) && (!G.IsDirected))
            {
                foreach (Edge<DataNode> E in Vi.InEdges)
                {
                    if (E.FromVertex == Vj)
                    {
                        E.Weight += 1;
                        //E.Label = E.Label + " " + Word;
                        Found = true;
                        break;
                    }
                }
            }
            if (!Found)
            {
                //need to make a new link
                G.ConnectVertices(Vi, Vj, "", 1.0f);
            }
        }

        /// <summary>
        /// Build a graph where keywords are the vertices and edges are the resources that share the keyword.
        /// Similar to MakeLinksGraph, but with vertices and edges the other way around.
        /// TODO: weight should reflect strength between two vertices, so multiple links should just increase weight
        /// CLIQUE GRAPH
        /// </summary>
        /// <returns></returns>
        public Graph<DataNode> KeywordOverviewGraph()
        {
            System.Diagnostics.Debug.WriteLine("Starting KeywordOverviewGraph");
            //most of this is copied from MakeKeywordLinks graph.
            //this one has keywords as vertices which are linked by edges of shared resources
            HashSet<string> StopWords = LoadStopWords(Path.Combine(DataRootDir, StopWordsFilename));
            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int DescriptionColIdx = Catalogue.Columns.IndexOf(Schema.DescriptionField);

            //Mapping between keyword and which resources use the keyword. Resource Id here is the line of the catalogue file.
            Dictionary<string, HashSet<int>> KeywordToResourceId = new Dictionary<string, HashSet<int>>();

            Graph<DataNode> G = new Graph<DataNode>(false);
            //now go through all the data and make a map of keywords used by resources
            float NumRows = (float)Catalogue.Rows.Count;
            System.Diagnostics.Debug.WriteLine("Number of resources in catalogue: " + NumRows);
            for (int ResourceId = 0; ResourceId < Catalogue.Rows.Count; ResourceId++)
            {
                DataRow Row = Catalogue.Rows[ResourceId];

                if (ResourceId % 100 == 0)
                    System.Diagnostics.Debug.WriteLine("ResourceId: " + ResourceId + " " + (float)ResourceId / NumRows * 100.0f + " %");
                //TODO: need safe title - no & characters
                string Title = Row[TitleColIdx] as string;
                string Description = Row[DescriptionColIdx] as string;

                //if there is no description then we can't do anything with this resource
                if (string.IsNullOrEmpty(Description)) continue;

                //remove all punctuation, convert to lowercase and split words
                //System.Diagnostics.Debug.WriteLine("Description=" + Description);
                Description = StripHtml(Description);
                string[] Words = SplitWords(Description);
                //then add a resource id entry for every keyword
                foreach (string Word in Words)
                {
                    if (!StopWords.Contains(Word))
                    {
                        if (!KeywordToResourceId.ContainsKey(Word))
                            KeywordToResourceId.Add(Word, new HashSet<int>()); //create word if new one
                        //add only if the word has not already been used with this resource id (i.e same word repeated in description)
                        //TODO: could strengthen the weight if word repeated, but Gephi seems to do this automatically where links are repeated (parallel edges?)
                        KeywordToResourceId[Word].Add(ResourceId); //add this resource to correct word entry
                        //System.Diagnostics.Debug.WriteLine("Word: " + Word + " Resource Id: " + ResourceId);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("Keyword counts:");
            foreach (KeyValuePair<string, HashSet<int>> KVP in KeywordToResourceId)
            {
                System.Diagnostics.Debug.WriteLine(KVP.Key + ": " + KVP.Value.Count);
            }

            //Create keyword vertices - need to do this before we start trying to connect them up
            for (int i = 0; i < KeywordToResourceId.Count; i++)
            {
                //Create a vertex for this keyword (i)
                DataNode UserData = new DataNode();
                UserData.ResourceId = i;
                string Keyword = KeywordToResourceId.ElementAt(i).Key;
                UserData.Title = Keyword;
                Vertex<DataNode> V = G.AddVertex(i, UserData);
                V.VertexLabel = Keyword; //annoying, but there's no other way to set the vertex label
            }

            //This is the code to create a graph where every vertex is a keyword and they are linked where two resources share that keyword
            for (int i = 0; i < KeywordToResourceId.Count; i++)
            {
                //if (i >= 40) break; //HACK!
                if (KeywordToResourceId.ElementAt(i).Value.Count < 200) continue; //only take vertices with >200 edges
                System.Diagnostics.Debug.WriteLine("Number of edges: " + G.NumEdges);
                System.Diagnostics.Debug.WriteLine("Processing Keyword "+i);
                HashSet<int> RID1 = KeywordToResourceId.ElementAt(i).Value;
                for (int j = i + 1; j < KeywordToResourceId.Count; j++)
                {
                    HashSet<int> RID2 = KeywordToResourceId.ElementAt(j).Value;

                    //now need to check the resources used by RID1 and RID2 to see if any are shared and these two keywords should be linked
                    //for (int p = 0; p < RID1.Count; p++)
                    //{
                    //    for (int q = p + 1; q < RID2.Count; q++)
                    //    {
                    //        if (RID1[p] == RID2[q])
                    //        {
                    //            //they share a resource, so link keywords i and j labelling with this resource id's title

                    //            //TODO: need safe title - no & characters
                    //            G.ConnectVertices(i, j, Catalogue.Rows[RID1[p]][TitleColIdx] as string, 1.0f);
                    //        }
                    //    }
                    //}

                    //hashset code
                    foreach (int id1 in RID1)
                    {
                        if (RID2.Contains(id1))
                        {
                            //they share a resource, so link keywords i and j labelling with this resource id's title
                            //need safe title - no & characters
                            string label = Catalogue.Rows[id1][TitleColIdx] as string;
                            label = label.Replace("&", " ");
                            //G.ConnectVertices(i, j, label, 1.0f);
                            //break; //only connect keywords once
                            SingleConnectVertex(ref G, i, j);
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("Cutting edges based on weight");
            for (int i = 0; i < G.Vertices.Count; i++)
            {
                Vertex<DataNode> V = G.Vertices[i];
                for (int e = 0; e < V.OutEdges.Count; e++)
                {
                    if (V.OutEdges[e].Weight < 2.0)
                    {
                        G.DeleteEdge(V.OutEdges[e]);
                        e--;
                    }
                }
            }

            //Go through all nodes and prune any that aren't connected by more than a certain threshold
            //A highly connected node could connect to one with a single connection, so drop the single connection vertex
            //OK, this is very similar to the orphan vertex code, but I might want to remove or change this later.
            System.Diagnostics.Debug.WriteLine("Cutting vertices based on connections");
            for (int i = 0; i < G.Vertices.Count; i++)
            {
                Vertex<DataNode> V = G.Vertices[i];
                //if ((V.OutEdges.Count + V.InEdges.Count)<100)
                if (G.w(V,V)<100) //weight is not the same as dimension
                {
                    G.DeleteVertex(V.VertexId);
                    --i;
                }
            }

            //now go back and delete any orphan vertices
            System.Diagnostics.Debug.WriteLine("Deleting orphan vertices");
            for (int i=0; i<G.Vertices.Count; i++)
            {
                Vertex<DataNode> V = G.Vertices[i];
                if ((V.OutEdges.Count == 0) && (V.InEdges.Count == 0))
                {
                    G.DeleteVertex(V.VertexId);
                    --i;
                }
            }

            System.Diagnostics.Debug.WriteLine("Vertices: " + G.NumVertices + " Edges: " + G.NumEdges);
            System.Diagnostics.Debug.WriteLine("Finished KeywordOverviewGraph");
            return G;
        }

        #endregion Graphs


        /// <summary>
        /// Delegate function to process each line of the csv file as it is read
        /// </summary>
        /// <param name="Headers"></param>
        /// <param name="Fields"></param>
        private void ProcessCSVLine(string[] Headers, string[] Fields)
        {
            //read _DataRowsToCheck (=10?) lines into the object and abort the load
            //DataLines.Add(Fields);
            //if (Loader.DataLineCount >= _DataRowsToCheck) Loader.Abort = true;
        }

        /// <summary>
        /// Copy of Microsoft function to change access permissions on file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="account"></param>
        /// <param name="rights"></param>
        /// <param name="controlType"></param>
        public static void AddFileSecurity(string fileName, string account, FileSystemRights rights, AccessControlType controlType)
        {
            // Get a FileSecurity object that represents the 
            // current security settings.
            FileSecurity fSecurity = File.GetAccessControl(fileName);

            // Add the FileSystemAccessRule to the security settings.
            fSecurity.AddAccessRule(new FileSystemAccessRule(account, rights, controlType));

            // Set the new access settings.
            File.SetAccessControl(fileName, fSecurity);
        }

        // Removes an ACL entry on the specified file for the specified account. 
        public static void RemoveFileSecurity(string fileName, string account, FileSystemRights rights, AccessControlType controlType)
        {
            // Get a FileSecurity object that represents the 
            // current security settings.
            FileSecurity fSecurity = File.GetAccessControl(fileName);

            // Remove the FileSystemAccessRule from the security settings.
            fSecurity.RemoveAccessRule(new FileSystemAccessRule(account, rights, controlType));

            // Set the new access settings.
            File.SetAccessControl(fileName, fSecurity);
        }


        /// <summary>
        ///  Upload all the information from the staging area to a database
        /// </summary>
        public virtual void DatabaseUpload(string ConnectionString)
        {
            GeometryFinder finder = new GeometryFinder(ConfigFilename);
            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            
            //make sure the data staging area exists
            Directory.CreateDirectory(DataStagingDir);

            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
            int UniqueKeyColIdx = Catalogue.Columns.IndexOf(Schema.UniqueKeyField);

            int count = 0;
            foreach (DataRow Row in Catalogue.Rows)
            {
                string Title = Row[TitleColIdx] as string;
                string DataLink = Row[LinkColIdx] as string;
                string UniqueKey = Row[UniqueKeyColIdx] as string;

                if (!string.IsNullOrEmpty(DataLink))
                {
                    //Data staging - download to the local file system and unzip if necessary
                    Uri StagedDataUri = StageData(new Uri(DataLink)); //this is either the root of the extracted zip hierarchy, or an actual file
                    Uri[] StagedDataFiles = FilterDataFiles(StagedDataUri); //get a list of files under the staging area that might contain data

                    float percent = (float)count / (float)DatasetCount * 100.0f;

                    //now get the files and analyse it
                    foreach (Uri FileUri in StagedDataFiles)
                    {
                        //we should have a true file (not dir) at this point and it should be a valid type as it's been filtered (*.csv)
                        if (FileUri.LocalPath.ToLower().EndsWith(".csv"))
                        {
                            Console.WriteLine("Staged File: " + FileUri.LocalPath);
                            //todo: create the table and write the data here
                            //MapTube.GIS.DataLoader loader = new MapTube.GIS.DataLoader();
                            //loader.LoadCSVData(

                            //use the more advanced MapTubeD loader
                            //MapTubeD.Utilities.DataLoader loader = new MapTubeD.Utilities.DataLoader();
                            //loader.ReadCSV(FileUri, ProcessCSVLine);
                            //loader.

                            //even more advanced geometry finder
                            string CreateCols = "";
                            //assume first column is the area key
                            CreateCols += "GeographyCode varchar(9)";
                            PointGeometryXYCRS probs = finder.ProbablePointGeometryFromDataFile(FileUri); //this does the column type scan
                            foreach (ColumnType ct in finder.ProbablePointDataColumnTypes)
                            {
                                if (ct.IsNumeric)
                                {
                                    CreateCols += "," + ct.Name + " real";
                                }
                            }
                            //now create the table
                            string TableName = "\"2011_"+Path.GetFileNameWithoutExtension(FileUri.LocalPath)+"\"";
                            string CreateSQL = "CREATE TABLE " + TableName + "(" + CreateCols + ")";
                            NpgsqlCommand cmd = new NpgsqlCommand(CreateSQL, conn);
                            cmd.ExecuteNonQuery();

                            //now copy the data in - need to make csv file readable by Everyone for this to work
                            AddFileSecurity(FileUri.LocalPath, @"\Everyone",FileSystemRights.Read, AccessControlType.Allow);
                            string CSVSQL = "COPY "+TableName+" FROM '"+FileUri.LocalPath+"' DELIMITER ',' CSV HEADER NULL AS '..'";
                            NpgsqlCommand csvcmd = new NpgsqlCommand(CSVSQL, conn);
                            csvcmd.ExecuteNonQuery();
                        }
                    }
                }
                ++count;
                Console.WriteLine("=======================================================================");
            }

        }

        public void Debug_WriteCatalogueRow(int RowNum)
        {
            DataRow Row = Catalogue.Rows[RowNum];
            foreach (DataColumn col in Catalogue.Columns)
            {
                System.Diagnostics.Debug.WriteLine(col.ColumnName + " : " + Row[col.ColumnName]);
            }
        }
    }
}
