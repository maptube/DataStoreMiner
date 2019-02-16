using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace DatastoreMiner
{
    class Correlation
    {
        private double[,] SpatialWeights;
        private double S0 = 0; //pre-calculated sum of all spatial weights

        /// <summary>
        /// This constructor provides a way of passing the centroids and pre-calculating a spatial weights matrix which speeds up the BivariateMoranI correlation.
        /// NOTE: the ovedering of this array MUST match the ordering of the X and Y vectors passed into the SpatialBivariateMoranIFast function.
        /// </summary>
        /// <param name="Centroids">Array of [x,y] values</param>
        public Correlation(double[,] Centroids)
        {
            int N = Centroids.GetLength(0);
            SpatialWeights = new double[N, N];
            for (int i=0; i<N; i++)
            {
                double CiX = Centroids[i, 0];
                double CiY = Centroids[i, 1];
                for (int j=0; j<N; j++)
                {
                    double CjX = Centroids[j, 0];
                    double CjY = Centroids[j, 1];
                    //calculate the spatial weight
                    double dx = CiX - CjX;
                    double dy = CiY - CjY;
                    double D = Math.Sqrt(dx * dx + dy * dy);
                    double W = 0;
                    if (D < 1) W = 1; //autocorrelation weight=1
                    else W = 1 / D; //otherwise the correlation weight is 1/D
                    SpatialWeights[i, j] = W;
                }
            }

            //pre-calculate sum of all spatial weights, which we need for MoranI
            this.S0 = 0;
            for (int i=0; i< N; i++)
            {
                for (int j=0; j< N; j++)
                {
                    this.S0 += this.SpatialWeights[i, j];
                }
            }
        }

        #region FAST methods

        /// <summary>
        /// Bivariate Moran I based on a pre-calculated spatial weights matrix (which is why it's not static like the other version).
        /// You don't need to pass in a centroids array as it's using this.SpatialWeights from the constructor, but you MUST ensure
        /// that X and Y are in step with the Centroids used to calcualte the spatial weights i.e. X[0], Y[0] and Centroid[0] all
        /// reference the same area. This should be obvious.
        /// </summary>
        /// <param name="X">Dataset X</param>
        /// <param name="Y">Dataset Y</param>
        /// <returns>Correlation coefficient [-1..+1]</returns>
        public double SpatialBivariateMoranIFast(double[] X, double[] Y)
        {
            System.Diagnostics.Debug.WriteLine("SpatialBivariateMoranIFast start");
            //Assert X.Length==Y.Length?
            //compute some stats on the X and Y sequences that we're going to need
            RunningStat rsx = new RunningStat();
            foreach (double value in X) rsx.Push(value);
            RunningStat rsy = new RunningStat();
            foreach (double value in Y) rsy.Push(value);
            double MeanX = rsx.Mean, SDX = rsx.StandardDeviation;
            double MeanY = rsy.Mean, SDY = rsy.StandardDeviation;

            //pre-calculate normalised X and Y to save some time on the big nested loop below
            double[] XN = new double[X.Length];
            double[] YN = new double[Y.Length];
            for (int i=0; i<X.Length; i++)
            {
                XN[i] = (X[i] - MeanX) / SDX;
                YN[i] = (Y[i] - MeanY) / SDY;
            }

            double Sum1 = 0, Sum2 = 0;
            //double S0 = 0;
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            double[] ParallelSum = new double[Y.Length]; 
            Parallel.For(0, YN.Length, i =>
            //for (int i = 0; i < YN.Length; i++)
            {
                ParallelSum[i] = 0;
                //Parallel.For(0, X.Length, j =>
                for (int j = 0; j < XN.Length; j++)
                {
                    double W = (float)this.SpatialWeights[i, j];
                    //Sum1 += Y[i] * W * X[j]; //version 1
                    //Sum2 += ((Y[i] - MeanY) / SDY) * W * ((X[i] - MeanX) / SDX); //version 2
                    //Sum2 += YN[i] * W * XN[j]; //version 2 optimised with pre-calculated normalised X and Y
                    ParallelSum[i]+= YN[i] * W * XN[j]; //version 3 optimised for parallel
                    //S0 += W; //sum of all weights
                }/*);*/
            });
            for (int i = 0; i < YN.Length; i++) Sum2 += ParallelSum[i]; //gather up all the parallel i sums into one - OK, you can use a summation kernel here...
            double I = Sum2 / this.S0; //was S0

            System.Diagnostics.Debug.WriteLine("SpatialBivariateMoranI finished: " + timer.ElapsedMilliseconds + " ms");
            return I;
        }

        #endregion FAST methods

        #region Static Methods

        /// <summary>
        /// Spatial Bivariate Moran I using
        /// I=Sigma_i(Sigma_j(Yi*Wij*Xj))/(S0*Sqrt(Variance(Y)*Variance(X)))
        /// where S0 is the sum of all the elements in W
        /// NOTE: X[i], Y[i] and Centroids[i] MUST all reference the same spatial area i.e. all three arrays are in step
        /// </summary>
        /// <param name="X">Data values of first table</param>
        /// <param name="Y">Data values of second table (must match X spatially)</param>
        /// <param name="Centroids">Centroid points of polygon areas to calculate distance weights (must match X and Y spatially)
        /// first point is ([0,0], [0,1]), second point is ([1,0],[1,1]). The reason for using the 2d double array in preference to
        /// an array of Point is the big increase in speed.</param>
        /// <returns></returns>
        public static double [] SpatialBivariateMoranI(double [] X, double [] Y, double [,] Centroids)
        {
            System.Diagnostics.Debug.WriteLine("SpatialBivariateMoranI start");
            //Assert X.Length==Y.Length?
            //compute some stats on the X and Y sequences that we're going to need
            RunningStat rsx = new RunningStat();
            foreach (double value in X) rsx.Push(value);
            RunningStat rsy = new RunningStat();
            foreach (double value in Y) rsy.Push(value);
            double MeanX=rsx.Mean, SDX=rsx.StandardDeviation;
            double MeanY=rsy.Mean, SDY=rsy.StandardDeviation;

            double Sum1 = 0, Sum2 = 0;
            double S0 = 0;
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            //Parallel.For(0, Y.Length, i =>
            for (int i=0; i<Y.Length; i++)
            {
                double CiX = Centroids[i, 0];
                double CiY = Centroids[i, 1];
                //Parallel.For(0, X.Length, j =>
                for (int j = 0; j < X.Length; j++)
                {
                    double dx = CiX - Centroids[j, 0];
                    double dy = CiY - Centroids[j, 1];
                    double D = Math.Sqrt(dx * dx + dy * dy); //SURELY 1/W !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    double W = 0;
                    if (D < 1) W = 1; //autocorrelation weight=1
                    else W = 1 / D; //otherwise the correlation weight is 1/D 
                    Sum1 += Y[i] * W * X[j]; //version 1
                    Sum2 += ((Y[i] - MeanY) / SDY) * W * ((X[i] - MeanX) / SDX); //version 2
                    S0 += W; //sum of all weights
                }/*);*/
            }/*);*/
            double I1 = Sum1 / (S0*Math.Sqrt(rsy.Variance*rsx.Variance));
            double I2 = Sum2 / S0;

            System.Diagnostics.Debug.WriteLine("SpatialBivariateMoranI finished: "+timer.ElapsedMilliseconds+" ms");
            return new double [] {I1, I2};
        }

        //TODO: alternate formula: (X-XBar)/SD ?

        /// <summary>
        /// Take the output file produced by Correlation and build a name lookup and matrix from the data in the file which can then
        /// be passed to the CorrelationMatrix class to create a gephi file or do any further analysis.
        /// Basically a link between the output file and analysis procedures.
        /// </summary>
        /// <param name="InFilename"></param>
        /// <param name="NameLookup"></param>
        /// <param name="Matrix"></param>
        public static void CorrelationMatrixFromFile(string InFilename, out Dictionary<int, string> NameLookup, out float[,] Matrix)
        {
            NameLookup = new Dictionary<int, string>();
            Dictionary<string,int> RNameLookup = new Dictionary<string, int>(); //reverse name lookup of above
            //first pass, make the variable name lookup table and get the variable count
            //this only uses the i table list of text names to come up with a unique list of variables in some sort of
            //sensible order as the Table_i and Var_i indexes are not monotonic
            using (TextReader reader = File.OpenText(InFilename))
            {
                string Line;
                Line = reader.ReadLine(); //skip header
                while ((Line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(Line)) continue;
                    string[] Fields = Line.Split(new char[] { ',' });
                    //24.5385134706347,0.977720119272712,0,0,1,3,KS101EW0001,KS101EW0003
                    //so that's I,I2,Table_i,Var_i,Table_j,Var_j,VarName_i,VarName_j
                    int Tablei = Convert.ToInt32(Fields[2]);
                    int Coli = Convert.ToInt32(Fields[3]);
                    int Tablej = Convert.ToInt32(Fields[4]);
                    int Colj = Convert.ToInt32(Fields[5]);
                    string VarName_i = Fields[6];
                    string VarName_j = Fields[7];
                    //if variable name doesn't exist in reverse lookup, then make a new column entry for the matrix
                    if (!RNameLookup.ContainsKey(VarName_j)) RNameLookup.Add(VarName_j,RNameLookup.Count);
                }
            }

            //now build the forward lookup for the matrix (col num->name) using the reverse lookup you just created
            //this is what gets returned
            foreach (KeyValuePair<string, int> KVP in RNameLookup)
            {
                NameLookup.Add(KVP.Value, KVP.Key);
            }

            //here we go again for the entire data file to build the matrix - ONLY LOWER DIAGONAL
            int Maxi = RNameLookup.Count-1;
            Matrix = new float[Maxi + 1, Maxi + 1];
            using (TextReader reader = File.OpenText(InFilename))
            {
                string Line;
                Line = reader.ReadLine(); //skip header
                while ((Line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(Line)) continue;
                    string[] Fields = Line.Split(new char[] { ',' });
                    //24.5385134706347,0.977720119272712,0,0,1,3,KS101EW0001,KS101EW0003
                    //so that's I,I2,Table_i,Var_i,Table_j,Var_j,VarName_i,VarName_j
                    float I2;
                    if (float.TryParse(Fields[1], out I2))
                    {
                        int Tablei = Convert.ToInt32(Fields[2]);
                        int Coli = Convert.ToInt32(Fields[3]);
                        int Tablej = Convert.ToInt32(Fields[4]);
                        int Colj = Convert.ToInt32(Fields[5]);
                        string VarName_i = Fields[6];
                        string VarName_j = Fields[7];
                        //need to use the reverse lookup here to convert text column names into matrix indexes
                        int i = RNameLookup[VarName_i];
                        int j = RNameLookup[VarName_j];
                        Matrix[i, j] = I2;
                    }
                }
            }
        }

        #endregion Static methods

    }
}
