using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

//STOLEN from MapTube

/// <summary>
/// Summary description for Jenks
/// As you might know, most of Jenks' optimization method
/// depends on Fischer's "EXACT OPTIMIZATION" METHOD in
/// (Fisher, W. D., 1958, On grouping for maximum homogeneity.
/// Journal of the American Statistical Association, 53, 789
/// ・98.).
/// This source code is available from following CMU's
/// statlib site in fortran code.
/// 
/// http://lib.stat.cmu.edu/cmlib/src/cluster/fish.f
/// 
/// Jenks' one is available in following paper media.
/// Probably its in Basic.
/// 
/// Jenks, G. F. (1977). Optimal data classification for
/// choropleth maps, Occasional paper No. 2. Lawrence, Kansas:
/// University of Kansas, Department of Geography. 
/// </summary>

namespace MapTube.GIS
{

    public class Jenks
    {
        public Jenks()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        //TODO: this returns positions in the data array, not threshold values
        //TODO: I don't think they're using the zero index in the array!
        public static float[] GetBreaks(int numclass, bool ContainsMissingData, float MissingDataValue, List<float> list)
        {
            //NOTE: as this dimensions two matrices of n x c where n=number of items in list and c=number of classes,
            //this is going to be very memory and CPU intensive for big datasets.

            //if there is missing data, then remove all the missing data values from the array first...
            list.Sort();
            if (ContainsMissingData)
            {
                Predicate<float> del = delegate(float value)
                {
                    return (Math.Abs(value - MissingDataValue) < float.Epsilon);
                };
                list.RemoveAll(del);
            }

            int numdata = list.Count();

            float[,] mat1 = new float[numdata + 1, numclass + 1];
            float[,] mat2 = new float[numdata + 1, numclass + 1];
            float[] st = new float[numdata];

            for (int i = 1; i <= numclass; i++)
            {
                mat1[1, i] = 1;
                mat2[1, i] = 0;
                for (int j = 2; j <= numdata; j++)
                    mat2[j, i] = float.MaxValue;
            }
            float v = 0;
            for (int l = 2; l <= numdata; l++)
            {
                float s1 = 0;
                float s2 = 0;
                float w = 0;
                for (int m = 1; m <= l; m++)
                {
                    int i3 = l - m + 1;

                    float val = list[i3 - 1];

                    s2 += val * val;
                    s1 += val;

                    w++;
                    v = s2 - (s1 * s1) / w;
                    int i4 = i3 - 1;
                    if (i4 != 0)
                    {
                        for (int j = 2; j <= numclass; j++)
                        {
                            if (mat2[l, j] >= (v + mat2[i4, j - 1]))
                            {
                                mat1[l, j] = i3;
                                mat2[l, j] = v + mat2[i4, j - 1];
                            }
                        }
                    }
                }
                mat1[l, 1] = 1;
                mat2[l, 1] = v;
            }
            int k = numdata;

            int[] kclass = new int[numclass];

            kclass[numclass - 1] = list.Count() - 1; //maximum value

            for (int j = numclass; j >= 2; j--)
            {
                System.Diagnostics.Debug.WriteLine("rank = " + mat1[k, j]);
                int id = (int)(mat1[k, j]) - 2;
                System.Diagnostics.Debug.WriteLine("val = " + list[id]);
                //System.out.println(mat2[k][j]);

                kclass[j - 2] = id;

                k = (int)mat1[k, j] - 1;
            }
            //return kclass;
            float[] breaks = new float[numclass];
            for (int i = 0; i < numclass; i++) breaks[i] = list[kclass[i]];
            return breaks;
        }

        //class doubleComp implements Comparator {
        //    public int compare(Object a, Object b) {
        //        if (((Double) a).doubleValue() < ((Double)b).doubleValue()) return -1;
        //        if (((Double) a).doubleValue() > ((Double)b).doubleValue()) return 1;
        //        return 0;
        //    }
        //}

    }
}