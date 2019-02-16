using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatastoreMiner
{
    /// <summary>
    /// Public class designed for running statistics metrics  on data (mainly on matrix 2d arrays).
    /// Primarily designed for comparing the KNN matrix data to the full spatial cross correlation data.
    /// NOTE: matrix formulas come from Quant on FMatrix statistics.
    /// </summary>
    public class Statistics
    {
        public static float Mean(float[,] X)
        {
            float Sum = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++) Sum += X[i, j];
            return Sum / X.Length;
        }

        public static float StandardDeviation(float[,] X, float Mean)
        {
            float SD = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++) SD += (float)Math.Pow(X[i, j] - Mean, 2);
            SD = (float)Math.Sqrt(SD / X.Length);
            return SD;
        }

        /// <summary>
        /// Simple correlation between two matrices.
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public static float MatrixCorrelate(float[,] X, float[,] Y)
        {
            float MeanX = Mean(X);
            float MeanY = Mean(Y);

            double r = 0;
            double C0 = 0, C1 = 0, C2 = 0, C3 = 0, C4 = 0;
            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    C0 = X[i, j] - MeanX;
                    C1 = Y[i, j] - MeanY;
                    C4 += C0 * C1;
                    C2 += Math.Pow(X[i, j] - MeanX, 2);
                    C3 += Math.Pow(Y[i, j] - MeanY, 2);
                }
            }
            //r = C0 * C1 / (Math.Sqrt(C2) * Math.Sqrt(C3));
            r = C4 / (Math.Sqrt(C2) * Math.Sqrt(C3));
            return (float)r;
        }

        public static float RootMeanSquareError(float[,] X, float[,] Y)
        {
            float Theta = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++) Theta += (float)Math.Pow(X[i, j] - Y[i, j], 2);
            Theta = (float)Math.Sqrt(Theta / X.Length);
            return Theta;
        }

        public static float ChiSquaredDifference(float[,] X, float[,] Y)
        {
            float Chi = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++)
                {
                    if ((X[i,j]!=0)&&(Y[i,j]!=0)) // 0/0 allowed, but a/0 is impossible->infinity
                        Chi += (float)Math.Pow(X[i, j] - Y[i, j], 2) / X[i, j];
                }
            Chi = (float)Math.Sqrt(Chi);
            return Chi;
        }

        public static float SorensonDiceIndex(float[,] X, float[,] Y)
        {
            float Phi = 0;
            float C0 = 0, C1 = 0;
            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    C0 += Math.Min(X[i, j], Y[i, j]);
                    C1 += Y[i, j];
                }
            }
            Phi = C0 / C1;
            return Phi;
        }

        public static float JaccardIndex(float[,] X, float[,] Y)
        {
            float Jaccard = 0;
            float C0 = 0, C1 = 0;
            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    C0 += Math.Min(X[i, j], Y[i, j]);
                    C1 += Math.Max(X[i, j], Y[i, j]);
                }
            }
            Jaccard = C0 / C1;
            return Jaccard;
        }

        public static float SumOfRatios(float[,] X, float[,] Y)
        {
            float Psi = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++)
                {
                    if ((X[i,j]!=0)&&(Y[i,j]!=0)) // 0/0=0, but 0/a error!
                        Psi += X[i, j] / Y[i, j];
                }
            return Psi;
        }

        public static float AverageRatio(float[,] X, float[,] Y)
        {
            float PsiBar = SumOfRatios(X, Y) / X.Length;
            return PsiBar;
        }

        public static float AbsoluteDifference(float[,] X, float[,] Y)
        {
            float G = 0;
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++) G += Math.Abs(X[i, j] - Y[i, j]);
            return G;
        }

        public static float AverageAbsoluteDifference(float[,] X, float[,] Y)
        {
            float GBar = AbsoluteDifference(X, Y) / X.Length;
            return GBar;
        }

        public static float AbsolutePercentDifference(float[,] X, float[,] Y)
        {
            float H = 0;
            //for (int i = 0; i < X.M; i++) for (int j = 0; j < X.N; j++) H += Math.Abs(X._M[i, j] - Y._M[i, j]) / Y._M[i, j];
            //New, no DIV#0
            for (int i = 0; i < X.GetLength(0); i++) for (int j = 0; j < X.GetLength(1); j++)
                {
                    if ((X[i, j] != 0) && (Y[i, j] != 0)) // 0/0=0, but 0/a error!
                        H += Math.Abs(X[i, j] - Y[i, j]) / X[i, j];
                }
            return H;
        }

        public static float AverageAbsolutePercentDifference(float[,] X, float[,] Y)
        {
            return AbsolutePercentDifference(X, Y) / X.Length;
        }

        public static float Entropy(float[,] X)
        {
            float E = 0;
            float Xlk = 0;
            for (int l = 0; l < X.GetLength(0); l++) for (int k = 0; k < X.GetLength(1); k++) Xlk += X[l, k];
            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    float pij = X[i, j] / Xlk;
                    //Yes, really, ==0.0f, with pij==0, the Log function is undefined.
                    //See: http://en.wikipedia.org/wiki/Entropy_(information_theory)
                    //for why 0 x log(0) = 0
                    if (pij != 0)
                        E -= pij * (float)Math.Log10(pij); //note minus
                }
            }
            return E;
        }

        public static float InformationDifference(float[,] X, float[,] Y)
        {
            float I = 0;

            float Xlk = 0;
            for (int l = 0; l < X.GetLength(0); l++) for (int k = 0; k < X.GetLength(1); k++) Xlk += X[l, k];

            float Ylk = 0;
            for (int l = 0; l < Y.GetLength(0); l++) for (int k = 0; k < Y.GetLength(1); k++) Ylk += Y[l, k];

            for (int i = 0; i < X.GetLength(0); i++)
            {
                for (int j = 0; j < X.GetLength(1); j++)
                {
                    float pijX = X[i, j] / Xlk;
                    float pijY = Y[i, j] / Ylk;
                    //same zero problem fix as before, 0 x log(0) =0
                    if ((pijX != 0.0f) && (pijY != 0.0f))
                        I += pijX * (float)Math.Log10(Math.Abs(pijX / pijY));
                    //System.Diagnostics.Debug.WriteLine(pijX + " " + pijY + " " + I);
                }
            }
            return I;
        }

    }
}
