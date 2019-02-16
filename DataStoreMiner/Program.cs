using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters;

namespace DatastoreMiner
{
    class Program
    {
        static void Main(string[] args)
        {
            //DataGovUk();
            
            NOMIS2011Census();

            //main program - we're either going to create a LondonDatastore class or a GovDatastore class here...
            //LondonDatastore ds = new LondonDatastore("..\\..\\Data\\images\\");
            //GovDatastore ds = new GovDatastore("..\\..\\Data\\images\\");           
        }

        /// <summary>
        /// data.gov.uk processing functions
        /// </summary>
        public static void DataGovUk()
        {
            GovDatastore ds = new GovDatastore();
            ds.ImageDirectory = "..\\..\\..\\Data\\images\\NOMIS2011Census\\";
            System.Diagnostics.Debug.WriteLine("Processed " + ds.DatasetCount + " rows");
            System.Diagnostics.Debug.WriteLine("CSV links: " + ds.CSVLinkCount + " zip links: " + ds.ZIPLinkCount);
            //for (int i=0; i<100; i++) ds.Debug_WriteCatalogueRow(i);
        }

        /// <summary>
        /// For the PhD results data, do all the comparisons between the full spatial correlate and the KNN correlate here
        /// to keep it all contained together.
        /// </summary>
        public static void MatrixComparisons()
        {
            Dictionary<int, string> Lookup;
            Dictionary<int, float[,]> KNNMatrix = new Dictionary<int, float[,]>(); //key is knn=3,4,5...9
            float[,] SCMatrix; //spatial correlate matrix
            string WorkingDir = "..\\..\\..\\Data\\images\\NOMIS2011Census\\";
            string[] KNNFilenames = {
                "processknearestneighbourcorrelate_3_20190209_121500.csv",
                "processknearestneighbourcorrelate_4_20190209_125000.csv",
                "processknearestneighbourcorrelate_5_20190209_135400.csv",
                "processknearestneighbourcorrelate_6_20190209_145700.csv",
                "processknearestneighbourcorrelate_7_20190209_154000.csv",
                "processknearestneighbourcorrelate_8_20190209_162700.csv",
                "processknearestneighbourcorrelate_9_20190209_171700.csv"
            };

            for (int k=0; k<7; k++)
            {
                Console.WriteLine("Loading " + KNNFilenames[k]);
                float[,] matrix;
                KNearestNeighbour.CorrelationMatrixFromFile(
                    Path.Combine(WorkingDir,KNNFilenames[k]),
                    out Lookup,
                    out matrix
                );
                KNNMatrix.Add(k+3, matrix);
            }
            //now the spatial correlate file
            Console.WriteLine("Loading processspatialcorrelatefast_cleaned_20180416.csv");
            KNearestNeighbour.CorrelationMatrixFromFile(
                Path.Combine(WorkingDir,"processspatialcorrelatefast_cleaned_20180416.csv"),
                out Lookup,
                out SCMatrix
            );
            //Clean data - need to remove NaN for statistics
            for (int k=3; k<=9; k++)
            {
                float[,] M = KNNMatrix[k];
                for (int x=0; x<M.GetLength(0); x++)
                {
                    for (int y=0; y<M.GetLength(1); y++)
                    {
                        if (float.IsNaN(M[x, y])) M[x, y] = 0;
                    }
                }
            }
            //And clean the SC Matrix
            for (int x = 0; x < SCMatrix.GetLength(0); x++) for (int y = 0; y < SCMatrix.GetLength(1); y++) if (float.IsNaN(SCMatrix[x, y])) SCMatrix[x, y] = 0;
            //OK, that's loaded all the matrix data, now do some analysis

            //correlate all the KNN matrices together
            //for (int i=3; i<=9; i++)
            //{
            //    for (int j=3; j<=9; j++)
            //    {
            //        float RMS = Statistics.RootMeanSquareError(KNNMatrix[i], KNNMatrix[j]);
            //        System.Diagnostics.Debug.WriteLine("MatrixComparisons,i=,{0},j=,{1},RMS=,{2}", i, j, RMS);
            //    }
            //}

            //then correlate KNN against SCMatrix
            //for (int k=3; k<=9; k++)
            //{
            //    float RMS = Statistics.RootMeanSquareError(SCMatrix,KNNMatrix[k]);
            //    System.Diagnostics.Debug.WriteLine("MatrixComparisons,SCMatrix,knn=,{0},RMS=,{1}", k, RMS);
            //}

            ////Bitmaps first - KNN9 and Spatial Correlate
            //Console.WriteLine("Making bitmaps");
            //Bitmap KNNImage = CorrelationMatrix.CreateMatrixImage(KNNMatrix[9]);
            //KNNImage.Save(Path.Combine(WorkingDir,"ImageMatrix_KNN9.png"));
            ////now the full spatial cross correlation
            //Bitmap SpatialCorrelateImage = CorrelationMatrix.CreateMatrixImage(SCMatrix);
            //SpatialCorrelateImage.Save(Path.Combine(WorkingDir, "ImageMatrix_SpatialCorrelate.png"));
            ////now make an error bitmap
            //float[,] EMatrix = new float[SCMatrix.GetLength(0),SCMatrix.GetLength(1)];
            //for (int x = 0; x < SCMatrix.GetLength(0); x++)
            //    for (int y = 0; y < SCMatrix.GetLength(1); y++)
            //        EMatrix[x, y] = (KNNMatrix[9][x, y] - SCMatrix[x, y])/SCMatrix[x,y];
            //Bitmap ErrorImage = CorrelationMatrix.CreateMatrixImage(EMatrix);
            //ErrorImage.Save(Path.Combine(WorkingDir, "ImageMatrix_EMatrix_KNN9.png"));
            ////End Bitmaps

            //comparisons of KNN3-9 against SCMatrix
            //System.Diagnostics.Debug.WriteLine("type,3,4,5,6,7,8,9");
            ////RMS
            //System.Diagnostics.Debug.Write("RMS,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.RootMeanSquareError(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////MatrixCorrelate
            //System.Diagnostics.Debug.Write("MatrixCorrelate,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.MatrixCorrelate(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////Chi Squared Diff
            //System.Diagnostics.Debug.Write("ChiSquaredDiff,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.ChiSquaredDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////SorensonDice
            //System.Diagnostics.Debug.Write("SorensonDiceIndex,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.SorensonDiceIndex(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////JaccardIndex
            //System.Diagnostics.Debug.Write("JaccardIndex,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.JaccardIndex(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////Sum of Ratios
            //System.Diagnostics.Debug.Write("SumOfRatios,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.SumOfRatios(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////AverageRatio
            //System.Diagnostics.Debug.Write("AverageRatio,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.AverageRatio(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////AbsoluteDifference
            //System.Diagnostics.Debug.Write("AbsoluteDifference,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.AbsoluteDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////AverageAbsoluteDifference
            //System.Diagnostics.Debug.Write("AverageAbsoluteDifference,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.AverageAbsoluteDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////AbsolutePercentDifference
            //System.Diagnostics.Debug.Write("AbsolutePercentDifference,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.AbsolutePercentDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////AverageAbsolutePercentDifference
            //System.Diagnostics.Debug.Write("AverageAbsolutePercentDifference,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.AverageAbsolutePercentDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");
            ////InformationDifference
            //System.Diagnostics.Debug.Write("InformationDifference,");
            //for (int k = 3; k <= 9; k++) System.Diagnostics.Debug.Write(string.Format("{0},", Statistics.InformationDifference(KNNMatrix[k], SCMatrix)));
            //System.Diagnostics.Debug.WriteLine("");

            //now graph methods
            System.Diagnostics.Debug.WriteLine("Average Degree");
            System.Diagnostics.Debug.WriteLine("ncut,3,4,5,6,7,8,9,SC");
            for (float ncut = -1.0f; ncut<=1.0f; ncut+=0.1f)
            {
                System.Diagnostics.Debug.Write(ncut + ",");
                for (int k=3; k<=9; k++)
                {
                    float deg = GraphStatistics.AverageDegreeAfterCut(KNNMatrix[k], ncut);
                    System.Diagnostics.Debug.Write(deg + ",");
                }
                float scdeg = GraphStatistics.AverageDegreeAfterCut(SCMatrix, ncut);
                System.Diagnostics.Debug.WriteLine(scdeg);
            }

            //and cluster counts based on varying the cut - NOTE: negative cuts only resulted in a single big cluster of everything, so start at 0.0 and work up to 1.0
            //NOTE: ClusterCut produces a lot of diagnostic information about the sizes of the groups in the clusters too
            System.Diagnostics.Debug.WriteLine("Cluster ncut count of groups formed");
            for (float ncut=0.0f; ncut<=1.0f; ncut+=0.1f)
            {
                System.Diagnostics.Debug.Write(ncut + ",");
                //System.Diagnostics.Debug.WriteLine("ncut=" + ncut);
                for (int k = 3; k <= 9; k++)
                {
                    System.Diagnostics.Debug.WriteLine("KNN=" + k);
                    float[,] matrix = KNNMatrix[k];
                    float count = GraphStatistics.ClusterCut(ref matrix, ncut);
                    System.Diagnostics.Debug.Write(count + ",");
                }
                System.Diagnostics.Debug.WriteLine("SCMATRIX");
                float sccount = GraphStatistics.ClusterCut(ref SCMatrix, ncut);
                System.Diagnostics.Debug.WriteLine(sccount);
            }
        }

        /// <summary>
        /// NOMIS 2011 Census bulk download processing functions
        /// </summary>
        public static void NOMIS2011Census()
        {
            NOMIS2011Census ds = new NOMIS2011Census();
            ds.ImageDirectory = "..\\..\\..\\Data\\images\\NOMIS2011Census\\";
            //System.Diagnostics.Debug.WriteLine("Dataset count = " + ds.DatasetCount + " CSV Count = " + ds.CSVLinkCount + " ZIP Count = " + ds.ZIPLinkCount);
            //ds.DatabaseUpload("Server=127.0.0.1;Port=5432;Database=;User Id=;Password=;");
            //ds.ProcessSpatialCorrelate();
            //ds.ProcessSpatialCorrelateFast();
            //ds.PostProcessCleanFast(new string[] {
            //    "processspatialcorrelatefast_static_0_18.csv",
            //    "processspatialcorrelatefast_dyn_19_35.csv",
            //    "processspatialcorrelatefast_dyn_36_59.csv",
            //    "processspatialcorrelatefast_dyn_60_113.csv",
            //    "processspatialcorrelatefast_dyn_114_409.csv",
            //    "processspatialcorrelatefast_dyn_410_550.csv",
            //    "processspatialcorrelatefast_dyn_551_991.csv",
            //    "processspatialcorrelatefast_dyn_878_2558.csv"
            //},
            //"processspatialcorrelatefast_cleaned.csv");
            //ds.ProcessKNearestNeighbourCorrelate(3);
            //ds.ProcessKNearestNeighbourCorrelate(4);
            //ds.ProcessKNearestNeighbourCorrelate(5);
            //ds.ProcessKNearestNeighbourCorrelate(6);
            //ds.ProcessKNearestNeighbourCorrelate(7);
            //ds.ProcessKNearestNeighbourCorrelate(8);
            //ds.ProcessKNearestNeighbourCorrelate(9);
            //NOMIS2011Census.PostProcessCleanKNN(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk5nearestneighbourcorrelate.csv",
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk5nearestneighbourcorrelate_cleaned.csv");
            //NOMIS2011Census.PostProcessClean(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\all_classification.csv",
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\all_classification_cleaned_NEW.csv");
            Dictionary<int, string> Lookup;
            float[,] matrix;
            //NOMIS2011Census.PostProcessGenerateTableRelationships(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\all_classification_cleaned.csv",
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\Table_CorrelationMatrix.csv",
            //    out Lookup,
            //    out matrix
            //);
            //Correlation.CorrelationMatrixFromFile(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\all_classification_cleaned.csv",
            //    out Lookup,
            //    out matrix
            //);
            //KNearestNeighbour.CorrelationMatrixFromFile(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk3nearestneighbourcorrelate_cleaned.csv",
            //    out Lookup,
            //    out matrix
            //);
            //KNearestNeighbour.CorrelationMatrixFromFile(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk4nearestneighbourcorrelate_cleaned.csv",
            //    out Lookup,
            //    out matrix
            //);
            //KNearestNeighbour.CorrelationMatrixFromFile(
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk5nearestneighbourcorrelate_cleaned.csv",
            //    out Lookup,
            //    out matrix
            //);
            MatrixComparisons(); //this runs the matrix comparison operations for the PhD corrections

            //write out gephi file
            //TODO: probably still want to substitute the table names from the lookup for plain text descriptions
            //NOTE: the tables correlation data was created from this, but with the threshold modified in CreateGexfFileFromCorrelationMatrix
            //so that all the edges were created, not just the positive which was used for the KNN and other data. The matrix also came
            //from PostProcessGenerateTableRelationships above.
            //CorrelationMatrix.CreateGexfFileFromCorrelationMatrix(
            //    -1.0f, Lookup, matrix,
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\all_classification_cleaned.gexf"
            //);
            //CorrelationMatrix.CreateGexfFileFromCorrelationMatrix(
            //    0.6f, Lookup, matrix,
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processspatialcorrelate_all_classification_cleaned.gexf"
            //);
            //KNN correlation matrix creation
            //CorrelationMatrix.CreateGexfFileFromCorrelationMatrix(
            //    0.6f, Lookup, matrix,
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk3nearestneighbourcorrelate_cleaned.gexf"
            //);
            //CorrelationMatrix.CreateGexfFileFromCorrelationMatrix(
            //    0.6f, Lookup, matrix,
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk4nearestneighbourcorrelate_cleaned.gexf"
            //);
            //CorrelationMatrix.CreateGexfFileFromCorrelationMatrix(
            //    0.6f, Lookup, matrix,
            //    @"C:\richard\stingray\talisman\trunk\Richard_PhD\data\7DataExploration\DatastoreMiner_NOMIS2011_CorrelationData\processk5nearestneighbourcorrelate_cleaned.gexf"
            //);
            //PDF Text extraction
            //ds.ProcessKeywordCorrelate();

            //Graph<DataNode> G = ds.MakeLinksGraph();
            //Graph<DataNode> G = ds.KeywordOverviewGraph();
            //G.WritePajekNETFile(Path.Combine(GovDatastore.DataRootDir,"DataGovUK.net"));
            //G.WriteGexfFile(Path.Combine(GovDatastore.DataRootDir,"DataGovUK.gexf"));

            //ds.Process();
            //Datastore.MakeBigImage(ds.ImageDirectory); //write out a big image of ALL the maps
            //ds.ImageMatch(); //check correspondence between all images
            //ds.AnalyseCorrelationData(); //bit of a hack this, goes through the imatch file and two index files to write out what matches in plain text

            //build a big correlation matrix from the correlation match file
            //CorrelationMatrix.CreateMatrixImage("..\\..\\Data\\images\\NOMIS2011Census\\GreenMatch\\imatch.txt", "CorrelationMatrix.jpeg");

            //build a Gephi file where every node is a dataset map image, which is connected to every other node by its correlation value
            //Dictionary<int, string> index = ds.GetDescriptionForIndex(); //get mapping between major-minor version number and plain text description
            //CorrelationMatrix.CreateGexfFile(5/*20*/,index,"..\\..\\Data\\images\\NOMIS2011Census\\GreenMatch\\imatch.txt", "CorrelationGraph.gexf");

            //Kohonen feature extraction
            //Dictionary<string, NetTopologySuite.Geometries.Point> centroids = ds.LoadCentroidsFromShapefile("..\\..\\..\\Data\\ONS\\MSOA_2011_EW_BGC_V2.shp");
            //Kohonen K = new Kohonen(centroids.Count, 4);
            //K.Process(ds.ImageDirectory);
            //K.LoadWeights("..\\..\\..\\Data\\images\\NOMIS2011Census\\kohonen_weights_30000.bin");
            //List<string> areas = centroids.Keys.ToList<string>();
            //K.WriteOutput(areas, "..\\..\\..\\Data\\images\\NOMIS2011Census\\kohonen_map.csv");
            //K.ClassifyAll(ds.ImageDirectory, "..\\..\\..\\Data\\images\\NOMIS2011Census\\kohonen_classify_nomis.csv");

            //MapTube map creation
            //MapTubeMapBuilder builder = new MapTubeMapBuilder(ds, "c:\\inetpub\\wwwroot\\App_Data\\geodatasources.xml");
            //builder.AddGeomHint("MSOA_2011", 2.0f); //this should come from the datastore really
            //builder.BaseURL = "http://www.maptube.org/census2011";
            //builder.Build("c:\\richard\\wxtemp\\census2011");
            //for (int mapid = 1380; mapid < 3933; ++mapid)
            //{
            //    System.Diagnostics.Debug.WriteLine("mapid="+mapid);
            //    MapTubeMapBuilder.RebuildMapImage(mapid);
            //    System.Threading.Thread.Sleep(1000);
            //}
        }
    }
}
