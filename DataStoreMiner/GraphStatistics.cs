using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatastoreMiner
{
    /// <summary>
    /// Used to count statistics on a graph (matrix) structure e.g. average degree after cut
    /// </summary>
    public class GraphStatistics
    {
        //public float[,] matrix;

        /// <summary>
        /// Initialise with an edge weights matrix
        /// </summary>
        /// <param name="matrix"></param>
        //public GraphStatistics(float[,] matrix)
        //{
        //    this.matrix = matrix;
        //}

        /// <summary>
        /// Counts average degree for all nodes after a weight cut operation.
        /// Essentially just count all the weights which are above the cut factor and then divide by the number of nodes.
        /// </summary>
        /// <param name="ncut"></param>
        /// <returns></returns>
        public static float AverageDegreeAfterCut(float[,] matrix, float ncut)
        {
            int count = 0;
            int M = matrix.GetLength(0);
            int N = matrix.GetLength(1);
            for (int x=0; x<M; x++)
            {
                for (int y=0; y<N; y++)
                {
                    if (matrix[x, y] > ncut) ++count;
                }
            }
            return ((float)count) / ((float)M); //M is number of nodes (also N)
        }

        /// <summary>
        /// Follow a node recursively, setting all reachable vertices to the same group number as V (the root vertex)
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="ncut"></param>
        /// <param name="V">Root Node</param>
        /// <param name="Group">List of nodes assigned to current cluster groups (-1=unassigned)</param>
        public static void ClusterTraverse(ref float[,] matrix, float ncut, int V, ref int[] Group)
        {
            //run through all the first neighbours of Vertex V by looking at his out edge list - these all get labelled with the current G
            for (int V2 = 0; V2 < matrix.GetLength(1); V2++) //so our edge being tested is from V->V2
            {
                if ((V != V2) && (matrix[V, V2] >= ncut)) //not taking any self edges, they're all 1.0, plus test weight for >=ncut
                {
                    //so, if V is currently in group G and there's a link between V->V2 that's good (>=ncut), then V2 must also be in G
                    //BUT, let's make absolutely sure first that there hasn't been an error - we're either assigning a new group, or over assigning with existing group == Group[V]
                    if ((Group[V2] != -1) && (Group[V2] != Group[V])) System.Diagnostics.Debug.WriteLine("Error! V=" + V + " V2=" + V2 + " V2G=" + Group[V2] + " VG=" + Group[V]);
                    if (Group[V2] == -1) //if we hit Group[V2]==Group[V], then it's a guard case and recursion stops
                    {
                        Group[V2] = Group[V]; //OK, vertex V2 is now in same group as V, so all his neighbours will also be in group V
                        ClusterTraverse(ref matrix, ncut, V2, ref Group); //follow links recursively with V2 as the new root
                    }
                }
            }
        }

        /// <summary>
        /// matrix is a network edge table which is symmetric.
        /// Returns number of clusters when graph is cut at the given weight
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="ncut">Cut graph, so weights below this are not followed</param>
        /// <returns></returns>
        public static int ClusterCut(ref float[,] matrix, float ncut)
        {
            int[] Group = new int[matrix.GetLength(0)];
            for (int i = 0; i < Group.Length; i++) Group[i] = -1;

            int G = 0; //current new group number for when we find an unassigned vertex

            //This loop looks for new root nodes that aren't yet assigned to a group.
            //When it finds one, it does the cluster traverse to exhaustively and recursively label all reachable nodes (based on ncut).
            for (int V = 0; V < matrix.GetLength(0); V++) //vertex number where we run through the table in the x direction looking for starting nodes
            {
                if (Group[V] == -1) //found one!
                {
                    Group[V] = G;
                    ++G;
                    ClusterTraverse(ref matrix, ncut, V, ref Group);
                }
            }

            //OK, write out some debugging information that might be useful - first count the numbers of vertices in each group cluster
            Dictionary<int, int> GroupCount = new Dictionary<int, int>();
            for (int i = 0; i < Group.Length; i++)
            {
                int GP = Group[i];
                if (GroupCount.ContainsKey(GP)) GroupCount[GP] = GroupCount[GP] + 1;
                else GroupCount[GP] = 1; //new group number
            }
            //System.Diagnostics.Debug.Write("ClusterCut::GroupCount,groupcounts,ncut="+ncut+",");
            //foreach (KeyValuePair<int, int> KVP in GroupCount)
            //{
            //    System.Diagnostics.Debug.Write(KVP.Value+",");
            //}
            //System.Diagnostics.Debug.WriteLine("");

            //the actual return value is just the number of separate clusters detected
            return GroupCount.Count;
        }
    }
}
