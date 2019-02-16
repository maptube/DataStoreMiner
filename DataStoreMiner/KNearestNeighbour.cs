using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.KdTree;

namespace DatastoreMiner
{
    /// <summary>
    /// Correlation based on k nearest neighbours to each zone.
    /// The idea is to pre-compute the k neighbours and then you only have to look them up.
    /// So construct with a centroids table and find the k nearest for every area.
    /// </summary>
    class KNearestNeighbour
    {
        protected int K; //the K in the K Nearest Neighbours
        protected Dictionary<string, string[]> Neighbours;

        public KNearestNeighbour(int k, Dictionary<string, NetTopologySuite.Geometries.Point> Centroids)
        {
            K = k;
            Neighbours = new Dictionary<string, string[]>();

            //build lookup of areakey,[k neighbours] which we keep for speed
            //using spatial index?
            //KdTree<string> index = new KdTree<string>();
            //foreach (KeyValuePair<string,Point> KVP in Centroids)
            //{
            //    index.Insert(KVP.Value.Coordinate, KVP.Key);
            //}
            //OK, this next bit is a fudge - you need a radius big enough to ensure you get K areas back
            //TODO: spatial index query and find K neighbours

            //Brute force approach, just go through everything, calculate all the distances and sort
            foreach (KeyValuePair<string, Point> KVPi in Centroids)
            {
                string AreaKeyi = KVPi.Key;
                Point C = KVPi.Value;
                Dictionary<string, float> Distances = new Dictionary<string, float>();
                foreach (KeyValuePair<string, Point> KVPj in Centroids)
                {
                    string AreaKeyj = KVPj.Key;
                    Point P = KVPj.Value;
                    double dx = C.X-P.X, dy = C.Y-P.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy); //you could use dist^2
                    Distances.Add(AreaKeyj, dist);
                }
                //sort here
                var sorted = from KVP in Distances orderby KVP.Value ascending select KVP;
                string[] kneighbours = new string[k];
                for (int i = 1; i <= K; i++) //NOTE i=0 is zero distance, so skip it
                {
                    kneighbours[i - 1] = sorted.ElementAt(i).Key;
                    //distance = sorted.ElementAt(i).Value for a check
                }
                Neighbours.Add(AreaKeyi, kneighbours);
            }
        }

        /// <summary>
        /// Correlate two tables using K nearest neighbours.
        /// NOTE: the X value is the base location, so the neighbours are looked up in Y.
        /// TODO: do you need to weight the neighbours differently to the central value?
        /// There are various ways of doing this. Here I'm using neighbours =0.5 but you could use centroid distances.
        /// </summary>
        /// <param name="areas">Area keys for the X and Y data arrays</param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public double Correlate(string[] areas, double[] X, double[] Y)
        {
            //go through each value of X, lookup the K nearest neighbours in Y and correlate
            //Basically, this is a copy of Correlation.SpatialBivariateMoranI but with the K bit added

            //compute some stats on the X and Y sequences that we're going to need
            RunningStat rsx = new RunningStat();
            foreach (double value in X) rsx.Push(value);
            RunningStat rsy = new RunningStat();
            foreach (double value in Y) rsy.Push(value);
            double MeanX = rsx.Mean, SDX = rsx.StandardDeviation;
            double MeanY = rsy.Mean, SDY = rsy.StandardDeviation;

            double Sum = 0;
            double S0 = 0; //sum of all weights
            //System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < X.Length; i++)
            {
                //do the central locations first
                double W;
                W = 1.0;
                Sum += ((Y[i] - MeanY) / SDY) * W * ((X[i] - MeanX) / SDX);
                S0 += W;

                //now the K neighbours;
                W=0.5;
                string[] KNs = Neighbours[areas[i]]; //K neighbours around area j
                for (int j = 0; j < K; j++)
                {
                    Sum +=
                        ((Y[i] - MeanY) / SDY) * W * ((X[i] - MeanX) / SDX);
                    S0 += W;
                }
            }
            double I = Sum / S0;

            return I;
        }

        /// <summary>
        /// Take the output file produced by KNN and build a name lookup and matrix from the data in the file which can then
        /// be passed to the CorrelationMatrix class to create a gephi file or do any further analysis.
        /// Basically a link between the output file and analysis procedures.
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="NameLookup"></param>
        /// <param name="Matrix"></param>
        public static void CorrelationMatrixFromFile(string InFilename, out Dictionary<int, string> NameLookup, out float[,] Matrix)
        {
            NameLookup = new Dictionary<int, string>();
            int Maxi = -1;
            //first pass, make the variable name lookup table and get the variable count
            using (TextReader reader = File.OpenText(InFilename))
            {
                string Line;
                Line = reader.ReadLine(); //skip header
                while ((Line=reader.ReadLine())!=null) {
                    if (string.IsNullOrEmpty(Line)) continue;
                    string[] Fields = Line.Split(new char[] { ',' });
                    //I,i,j,VarName_i,VarName_j,milliseconds
                    //0.999861130398543,0,0,KS101EW0001,KS101EW0001,10
                    //float I;
                    //if (float.TryParse(Fields[0],out I))
                    //{
                        int i = Convert.ToInt32(Fields[1]);
                        int j = Convert.ToInt32(Fields[2]);
                        //if (j < Maxi) break; //assume j is monotonic increasing as a performance gain
                        string VarName_i = Fields[3];
                        string VarName_j = Fields[4];
                        if (i > Maxi) Maxi = i;
                        if (j > Maxi) Maxi = j;
                        if (!NameLookup.ContainsKey(i)) NameLookup.Add(j, VarName_j);
                    //}
                }
            }
            //make sure all the NameLookup index values are there from 0..MaxI (i.e. if you get a NaN)
            for (int i = 0; i <= Maxi; i++)
            {
                if (!NameLookup.ContainsKey(i)) NameLookup.Add(i, "Missing_" + i);
                System.Diagnostics.Debug.WriteLine("Missing lookup for " + i);
            }

            //here we go again for the entire data file to build the matrix (WAS - ONLY LOWER DIAGONAL, but now switched it to BOTH)
            Matrix = new float[Maxi+1, Maxi+1];
            using (TextReader reader = File.OpenText(InFilename))
            {
                string Line;
                Line = reader.ReadLine(); //skip header
                while ((Line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(Line)) continue;
                    string[] Fields = Line.Split(new char[] { ',' });
                    //I,i,j,VarName_i,VarName_j,milliseconds
                    //0.999861130398543,0,0,KS101EW0001,KS101EW0001,10
                    float I;
                    if (float.TryParse(Fields[0], out I))
                    {
                        int i = Convert.ToInt32(Fields[1]);
                        int j = Convert.ToInt32(Fields[2]);
                        //string VarName_i = Fields[3];
                        //string VarName_j = Fields[4];
                        Matrix[i, j] = I;
                        Matrix[j, i] = I; //AND UPPER DIAGONAL
                    }
                }
            }
        }

    }
}
