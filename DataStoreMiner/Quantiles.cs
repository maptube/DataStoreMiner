using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MapTube.GIS
{
    /// <summary>
    /// Summary description for Quantiles
    /// </summary>
    public class Quantiles
    {
        //public Quantiles()
        //{
        //    //
        //    // TODO: Add constructor logic here
        //    //
        //}


        /// <summary>
        /// Get Quantile breaks for a list of floating point data values that might contain a value signifying a missing
        /// data value.
        /// </summary>
        /// <param name="NumClass">The number of classes to get breaks for</param>
        /// <param name="ContainsMissingData">True if there is a missing data value e.g. -1, 999 etc</param>
        /// <param name="MissingDataValue">The value of the missing data e.g. -1, 999 etc</param>
        /// <param name="Values">The dataset as a list of floats that may contain missing data values</param>
        /// <returns>An array of the quantile breaks in the data for the specified number of classes.
        /// NOTE: this always returns an array of NumClass+1 where the first value is the minimum and the
        /// last value is the maximum.</returns>
        public static float[] GetBreaks(int NumClass, bool ContainsMissingData, float MissingDataValue, List<float> Values)
        {
            //get the quantile cutoff thresholds for a specified number of groups as an array of floats
            //e.g. NumGroups=4 => Quartiles, 5=>Qunitiles, 10=>Centiles, 100=>Percentiles

            float[] quantiles = new float[NumClass + 1];
            Values.Sort(); //TODO: check that this is ascending order

            //if there is missing data, then remove all the missing data values from the array first...
            if (ContainsMissingData)
            {
                Predicate<float> del = delegate(float value)
                {
                    return (Math.Abs(value - MissingDataValue) < float.Epsilon);
                };
                Values.RemoveAll(del);
            }

            quantiles[0] = Values[0]; //Q[0] is the minimum value
            quantiles[NumClass] = Values[Values.Count - 1]; //Q[NumClass] is the maximum value

            int n = Values.Count;
            for (int quant = 1; quant < NumClass; quant++)
            {
                int k = (int)(((float)quant / (float)NumClass) * (n - 1));
                float f = ((float)quant / (float)NumClass) * (n - 1) - (float)k;
                //System.out.println("k=" + k + " f=" + f);

                float result = 0;
                if (k + 1 >= n)
                {
                    //if either k or k+1 exceed array bounds, then return this quantile
                    //as the maximum value in the array
                    result = Values[n - 1];
                }
                else
                {
                    result = Values[k] + f * (Values[k + 1] - Values[k]);
                }
                //System.out.println("quart=" + quart + " value=" + result);
                quantiles[quant] = result;
            }
            return quantiles;
        }
    }
}