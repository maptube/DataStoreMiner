using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatastoreMiner
{

    /// <summary>
    /// Compute running mean and variance using recurrance formulas.
    /// See: Knuth, The Art of Computer Programming, Vol 2 3rd edition, page 232
    /// </summary>
    public class RunningStat
    {
        private int m_n = 0;
        private double m_oldM = 0, m_newM = 0, m_oldS = 0, m_newS = 0;

        #region properties
        public int NumDataValues
        {
            get { return m_n; }
        }

        public double Mean
        {
            get { return (m_n > 0) ? m_newM : 0.0; }
        }

        public double Variance
        {
            get { return ((m_n > 1) ? m_newS / (m_n - 1) : 0.0); }
        }

        public double StandardDeviation
        {
            get { return Math.Sqrt(Variance); }
        }

        #endregion properties

        public RunningStat() { }

        public void Clear()
        {
            m_n = 0;
        }

        /// <summary>
        /// Main procedure to push a new data value and recalculate the current mean and variance
        /// </summary>
        /// <param name="x"></param>
        public void Push(double x)
        {
            m_n++;
            if (m_n == 1)
            {
                m_oldM = m_newM = x;
                m_oldS = 0.0;
            }
            else
            {
                m_newM = m_oldM + (x - m_oldM) / m_n;
                m_newS = m_oldS + (x - m_oldM) * (x - m_newM);

                m_oldM = m_newM;
                m_oldS = m_newS;
            }
        }

        /// <summary>
        /// Combine the sample number, mean and std dev population passed as parameters with the current data
        /// </summary>
        /// <param name="n">Number of items in the second population</param>
        /// <param name="mu">Mean of the second population</param>
        /// <param name="stdDev">Standard deviation of the second population</param>
        public void PushPopulation(int n, double mu, double stdDev)
        {
            if (m_n == 0)
            {
                m_n = n;
                m_newM = mu;
                m_newS = Math.Pow(stdDev, 2) * (m_n - 1);
            }
            else
            {
                double muAB = CombineMeans(m_n, this.Mean, n, mu);
                double sigmaSqAB = CombineVariance(m_n, this.Mean, this.Variance, n, mu, Math.Pow(stdDev, 2), muAB);
                m_n += n; //add the new number of samples
                m_newM = muAB; //set the new mean
                m_newS = sigmaSqAB * (m_n - 1); //set the new sum of squares
            }
        }

        /// <summary>
        /// Combine the means from two populations
        /// </summary>
        /// <param name="na">The number of samples in population A</param>
        /// <param name="muA">The mean of population A</param>
        /// <param name="nb">The number of samples in population B</param>
        /// <param name="muB">The mean of population B</param>
        /// <returns>The combined mean of populations A and B together</returns>
        public static double CombineMeans(double na, double muA, double nb, double muB)
        {
            return ((na * muA) + (nb * muB)) / (na + nb);
        }

        /// <summary>
        /// Combine standard deviations from two populations
        /// </summary>
        /// <param name="na">The number of samples in population A</param>
        /// <param name="muA">The mean of population A</param>
        /// <param name="stdDevA">The standard deviation of population A</param>
        /// <param name="nb">The number of samples in population B</param>
        /// <param name="muB">The mean of population B</param>
        /// <param name="stdDevB">The standard deviation of population B</param>
        /// <param name="muAB">The combined mean of population A and B, in other words call CombineMeans(na,muA,nb,muB) first</param>
        /// <returns>The combined standard deviation of populations A and B together</returns>
        public static double CombineStdDevs(double na, double muA, double stdDevA, double nb, double muB, double stdDevB, double muAB)
        {
            double sigmaSqA = Math.Pow(stdDevA, 2); //variance A
            double sigmaSqB = Math.Pow(stdDevB, 2); //variance B
            //calculate variance of A and B
            double sigmaSqAB = na * (sigmaSqA + Math.Pow(muA - muAB, 2)) / (na + nb)
                + nb * (sigmaSqB + Math.Pow(muB - muAB, 2)) / (na + nb);
            return Math.Sqrt(sigmaSqAB);
        }

        //same as stddev, but uses and returns variance
        public static double CombineVariance(double na, double muA, double sigmaSqA, double nb, double muB, double sigmaSqB, double muAB)
        {
            //calculate variance of A and B
            double sigmaSqAB = na * (sigmaSqA + Math.Pow(muA - muAB, 2)) / (na + nb)
                + nb * (sigmaSqB + Math.Pow(muB - muAB, 2)) / (na + nb);
            return sigmaSqAB;
        }

    }

}
