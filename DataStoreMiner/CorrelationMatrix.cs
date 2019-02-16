using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace DatastoreMiner
{
    /// <summary>
    /// Build a correlation matix image from an imatch data file
    /// Data must be in the format: I1,I2,A1,A2,B1,B2,NAME1,NAME2,D
    /// I1: index number of variable 1, which goes from 0..MaxVariables irrespective of which dataset the variable column comes from
    /// I2: same as I1, but for the other variable column being compared
    /// A1: Dataset number, which goes from 0..MaxDatasets (MaxDatasets=number of files being analysed in this case)
    /// A2: Variable number in dataset A1, which goes from 0..NumberOfDataColumns in the table (dataset) A1
    /// B1: same as A1 for the other variable being compared
    /// B2: same as B2 for the other variable being compared
    /// NAME1: filename of the first image being compared from I1,A1,A2 data
    /// NAME2: filename of the second image being compared from I2,B1,B2 data
    /// D: correlation value using some method to compare the data from the files in NAME1 and NAME2 maps
    /// </summary>
    class CorrelationMatrix
    {
        //format a string to be safe for an XML document
        private static string SafeXML(string Text)
        {
            return Text.Replace("&", "&amp;");
        }

        /// <summary>
        /// Read the data from DataFilename and generate an image with one pixel per correlation value
        /// </summary>
        /// <param name="DataFilename">imatch.txt</param>
        /// <param name="OutputImageFilename">A jpeg output</param>
        public static void CreateMatrixImage(string DataFilename, string OutputImageFilename)
        {
            TextReader reader = File.OpenText(DataFilename);
            string Line = "";
            
            //Step 1: we need to find out how many values there are, so need to read I2 column until it goes back to zero (faster than reading I1)
            int Dimension = -1;
            while ((Line = reader.ReadLine()) != null)
            {
                string[] fields = Line.Split(new char[] { ',' });
                if (fields.Length == 9)
                {
                    int I2 = Convert.ToInt32(fields[1]);
                    if (I2 >= Dimension) Dimension = I2;
                    else break; //break out of loop as the I2 value has now dropped, so we know the max dimension
                }
            }
            reader.Close();

            //create a bitmap of the right dimension (remember Dimension is the MAX value)
            Bitmap image = new Bitmap(Dimension+1, Dimension+1);

            //can't reset the stream, so need to open the reader again to get back to the start
            reader = File.OpenText(DataFilename);
            while ((Line = reader.ReadLine()) != null)
            {
                string[] fields = Line.Split(new char[] { ',' });
                if (fields.Length == 9)
                {
                    int I1 = Convert.ToInt32(fields[0]);
                    int I2 = Convert.ToInt32(fields[1]);
                    int A1 = Convert.ToInt32(fields[2]);
                    int A2 = Convert.ToInt32(fields[3]);
                    int B1 = Convert.ToInt32(fields[4]);
                    int B2 = Convert.ToInt32(fields[5]);
                    string F1 = fields[6];
                    string F2 = fields[7];
                    float D = Convert.ToSingle(fields[8]);
                    System.Diagnostics.Debug.WriteLine("Writing: " + I1 + "," + I2);
                    //after all that, we only plot a single point
                    Color Col = Color.FromArgb(255,(int)D,(int)D,(int)D);
                    image.SetPixel(I1, I2, Col);
                }
            }
            reader.Close();

            image.Save(OutputImageFilename, ImageFormat.Jpeg);

        }

        /// <summary>
        /// Create an image file where one pixel is the intensity of the link between the two vertices in the matrix of edge weights.
        /// Assumes weights -1..+1 (but clamps if over range)
        /// </summary>
        /// <param name=""></param>
        /// <returns>A Bitmap containing full blue for -1 and full red for +1 with black in the middle, plus linearly interpolated shades inbetween</returns>
        public static Bitmap CreateMatrixImage(float[,] Matrix)
        {
            //create a bitmap of the right dimension (remember Dimension is the MAX value)
            Bitmap image = new Bitmap(Matrix.GetLength(0) + 1, Matrix.GetLength(1) + 1);

            for (int y=0; y<Matrix.GetLength(1); y++)
            {
                for (int x=0; x<Matrix.GetLength(0); x++)
                {
                    float val = Matrix[x, y];
                    if (val < -1.0) val = 1.0f;
                    else if (val > 1.0) val = 1.0f;
                    Color Col = Color.Black;
                    if (float.IsNaN(val)) Color.FromArgb(255, 255, 255, 255);
                    else if (val < 0) Col = Color.FromArgb(255, 0, 0, ((int)(-val * 255)));
                    else Col = Color.FromArgb(255, ((int)(val * 255)), 0, 0);
                    image.SetPixel(x, y, Col);
                }
            }

            return image;
        }

        /// <summary>
        /// Take the imatch correlation data and write out a Gephi file where every node is a map and connects to every other node with a
        /// weight equal to its correlation score.
        /// </summary>
        /// <param name="Threshold">Threshold for distance - anything greater than this gets cut</param>
        /// <param name="Index">Mapping between index (0..2558, not major-minor) version number and plain text description of the dataset</param>
        /// <param name="DataFilename">imatch.txt</param>
        /// <param name="OutputGraphFilename">The gexf file to write</param>
        public static void CreateGexfFile(float Threshold, Dictionary<int,string> Index, string DataFilename, string OutputGraphFilename)
        {
            //<?xml version="1.0" encoding="UTF-8"?>
            //<gexf xmlns="http://www.gexf.net/1.2draft" version="1.2">
            //<meta lastmodifieddate="2009-03-20">
            //<creator>Gexf.net</creator>
            //<description>A hello world! file</description>
            //</meta>
            //<graph mode="static" defaultedgetype="directed">
            //<nodes>
            //<node id="0" label="Hello" />
            //<node id="1" label="Word" />
            //</nodes>
            //<edges>
            //<edge id="0" source="0" target="1" />
            //</edges>
            //</graph>
            //</gexf>

            //could use an xml serializer, but it's just a (very big) list of nodes and edges, so going to do it the quick way
            using (TextWriter writer = File.CreateText(OutputGraphFilename))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.WriteLine("<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">");
                writer.WriteLine("<meta lastmodifieddate=\"" + DateTime.Now.ToShortTimeString() + "\">");
                writer.WriteLine("<creator>CASA</creator>");
                writer.WriteLine("<description>Graph</description>");
                writer.WriteLine("</meta>");
                writer.WriteLine("<graph mode=\"static\" defaultedgetype=\"undirected\">");
                TextReader reader = File.OpenText(DataFilename);
                string Line = "";

                //Step 1: we need to find out how many values there are, so need to read I2 column until it goes back to zero (faster than reading I1)
                int Dimension = -1;
                while ((Line = reader.ReadLine()) != null)
                {
                    string[] fields = Line.Split(new char[] { ',' });
                    if (fields.Length == 9)
                    {
                        int I2 = Convert.ToInt32(fields[1]);
                        if (I2 >= Dimension) Dimension = I2;
                        else break; //break out of loop as the I2 value has now dropped, so we know the max dimension
                    }
                }
                reader.Close();

                //write out the node information
                writer.WriteLine("<nodes>");
                for (int VertexId = 0; VertexId < Dimension; ++VertexId)
                {
                    string Label = Index[VertexId];
                    writer.WriteLine(
                        string.Format("<node id=\"{0}\" label=\"{1}\" />", VertexId, SafeXML(Label))); //TODO: label needs to be xml encoded
                }
                writer.WriteLine("</nodes>");

                //now move on to the edges
                writer.WriteLine("<edges>");
                int EdgeId = 0;
                //can't reset the stream, so need to open the reader again to get back to the start
                reader = File.OpenText(DataFilename);
                while ((Line = reader.ReadLine()) != null)
                {
                    string[] fields = Line.Split(new char[] { ',' });
                    if (fields.Length == 9)
                    {
                        int I1 = Convert.ToInt32(fields[0]);
                        int I2 = Convert.ToInt32(fields[1]);
                        int A1 = Convert.ToInt32(fields[2]);
                        int A2 = Convert.ToInt32(fields[3]);
                        int B1 = Convert.ToInt32(fields[4]);
                        int B2 = Convert.ToInt32(fields[5]);
                        string F1 = fields[6];
                        string F2 = fields[7];
                        float D = Convert.ToSingle(fields[8]);

                        if (D >= Threshold) continue;
                        
                        if (I1 < I2)
                        {
                            //only write out lower triangle (no A=B and B=A or A=A references)
                            System.Diagnostics.Debug.WriteLine("Writing: " + I1 + "," + I2);
                            writer.WriteLine(
                                string.Format("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\" weight=\"{3}\" label=\"{4}\" />",EdgeId, I1, I2, 1/D, D /*the label*/));
                            ++EdgeId;
                        }
                    }
                }
                reader.Close();
                writer.WriteLine("</edges>");

                //finished writing data, now the postamble
                writer.WriteLine("</graph>");
                writer.WriteLine("</gexf>");
                writer.Close();
            }
        }

        /// <summary>
        /// do the more general task of creating a Gephi Gexf file from a 2D correlation matrix.
        /// </summary>
        /// <param name="Threshold">
        /// NOTE: threshold parameter is reversed from CreateGexfFile as this is correlation, not distance
        /// Math.Abs(Iij) is compared to this threshold and anything below is cut. Set to negative value if you want everything.
        /// </param>
        /// <param name="Index">Dataset index number (row or col) to Label Name mapping</param>
        /// <param name="Matrix">N x N matrix of Iij correlation values</param>
        /// <param name="OutputGraphFilename">Filename to write</param>
        public static void CreateGexfFileFromCorrelationMatrix(float Threshold, Dictionary<int,string> Index, float[,] Matrix, string OutputGraphFilename)
        {
            //This is just a copy of the function it overloads with a few modifications.
            
            //preconditions?
            //check Matrix is square (and symmetric if unordered links?)
            //check matrix dimension is the same as Index dimension

            int NumEdges = 0;

            //could use an xml serializer, but it's just a (very big) list of nodes and edges, so going to do it the quick way
            using (TextWriter writer = File.CreateText(OutputGraphFilename))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.WriteLine("<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">");
                writer.WriteLine("<meta lastmodifieddate=\"" + DateTime.Now.ToShortTimeString() + "\">");
                writer.WriteLine("<creator>CASA</creator>");
                writer.WriteLine("<description>Graph</description>");
                writer.WriteLine("</meta>");
                writer.WriteLine("<graph mode=\"static\" defaultedgetype=\"undirected\">");

                //Step 1: we need to find out how many values there are, so need to read I2 column until it goes back to zero (faster than reading I1)
                int Dimension = Matrix.GetLength(0); //==GetLength(1)

                //write out the node information
                writer.WriteLine("<nodes>");
                for (int VertexId = 0; VertexId < Dimension; ++VertexId)
                {
                    string Label = Index[VertexId];
                    writer.WriteLine(
                        string.Format("<node id=\"{0}\" label=\"{1}\" />", VertexId, SafeXML(Label))); //TODO: label needs to be xml encoded
                }
                writer.WriteLine("</nodes>");

                //now move on to the edges
                writer.WriteLine("<edges>");
                int EdgeId = 0;
                for (int i=0; i<Dimension; i++) {
                    for (int j=i+1; j<Dimension; j++) { //only write out lower triangle (no A=B and B=A or A=A references)
                        float Iij = Matrix[i,j]; //correlation value, I
//changed threshold to only upper tail                        
                        //if (Math.Abs(Iij) >= Threshold)
                        if (Iij>=Threshold)
                        {
                            System.Diagnostics.Debug.WriteLine("Writing: " + i + "," + j);
                            writer.WriteLine(
                                string.Format("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\" weight=\"{3}\" label=\"{4}\" />",
                                EdgeId, i, j, Iij, Iij /*the label*/));
                            ++EdgeId;
                            ++NumEdges;
                        }
                    }
                }
                writer.WriteLine("</edges>");

                //finished writing data, now the postamble
                writer.WriteLine("</graph>");
                writer.WriteLine("</gexf>");
                writer.Close();
            }
            System.Diagnostics.Debug.WriteLine("CreateGexfFileFromCorrelationMatrix: EdgeId=" + NumEdges);
        }
        

    }

}
