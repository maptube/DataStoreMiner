using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DatastoreMiner
{
    /// <summary>
    /// Kohonen self organised feature map. Haykin Ch 10.5
    /// Operates on the input matrix containing all the data.
    /// TODO: add a neuron interpolation function
    /// </summary>
    class Kohonen
    {
        public int InputNeurons = 0;
        public int OutputDimension = 0;
        //todo: need neta, learning rate
        public double[,,] W;
        //private double[,,] deltaW;

        private Random trainingRnd = new Random(); //picks the random training set to present

        //outputs
        int WinX, WinY;
        double WinArg;
        double[,] OutputNeurons;
        int Epoch;

        //internal statistics
        double eAll, eMin, eMax; //min and max RMS error for individual datasets from CalculateError

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="j">Number of inputs i.e. MSOA areas</param>
        /// <param name="n">Number of outputs neurons, forming an n x n square</param>
        public Kohonen(int j, int n)
        {
            InputNeurons = j;
            OutputDimension = n;
            //define weights and randomise them
            W = new double[OutputDimension, OutputDimension, InputNeurons];
            //deltaW = new double[OutputDimension, OutputDimension, InputNeurons];
            OutputNeurons = new double[OutputDimension, OutputDimension];
            Random rnd = new Random();
            for (int y = 0; y < OutputDimension; y++)
            {
                for (int x = 0; x < OutputDimension; x++)
                {
                    for (int i = 0; i < InputNeurons; i++)
                    {
                        //TODO: this is -1..1, what should the range be? Should be similar to input data.
                        W[x, y, i] = rnd.NextDouble()*2.0-1.0;
                        //W[x, y, i] = rnd.NextDouble();
                    }
                }
            }
        }

        public void SaveWeights(string Filename)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream writer = new FileStream(Filename, FileMode.Create))
            {
                formatter.Serialize(writer,W);
            }
        }

        public void LoadWeights(string Filename)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream reader = new FileStream(Filename, FileMode.Open))
            {
                W = (double[,,])formatter.Deserialize(reader);
            }
        }

        /// <summary>
        /// Present an input to the network and find the winning neuron.
        /// </summary>
        /// <param name="Input"></param>
        public void Forward(double[] Input)
        {
            //these are the outputs from this function
            WinX = -1;  WinY = -1;
            WinArg = double.MaxValue;

            for (int y = 0; y < OutputDimension; y++)
            {
                for (int x = 0; x < OutputDimension; x++)
                {
                    double Arg = 0;
                    for (int j = 0; j < InputNeurons; j++)
                    {
                        Arg += Math.Abs(Input[j] - W[x, y, j]);
                    }
                    OutputNeurons[x, y] = Arg; //save it in case we need it later
                    if (Arg < WinArg)
                    {
                        WinArg = Arg;
                        WinX = x;
                        WinY = y;
                    }
                }
            }
            //System.Diagnostics.Debug.WriteLine("Win: " + WinX + "," + WinY);
        }

        /// <summary>
        /// Calls Forward on a pattern to get WinX, WinY and WinArg, this then updates the weights in the neighbourhood of the
        /// winning neuron to move it closer to the input pattern.
        /// </summary>
        /// <param name="LearnRate">Rate of change of weights</param>
        /// <param name="NeighbourDistance">Distance threshold for learning in output 2D space</param>
        /// <param name="Input">The input vector just applied to Forward</param>
        /// <returns>Sum of magnitude of changes made to weights i.e. for convergence tests</returns>
        public double Backward(double LearnRate, double NeighbourDistance, double [] Input)
        {
            //Forward propagate the pattern
            Forward(Input);

            //calculate error for winning neuron only for convergence
            double ErrorDelta = 0;
            for (int j = 0; j < InputNeurons; j++) ErrorDelta += Math.Abs(Input[j] - W[WinX, WinY, j]);

            //now learn
            for (int y = 0; y < OutputDimension; y++)
            {
                float dy=y-WinY;
                for (int x = 0; x < OutputDimension; x++)
                {
                    float dx=x-WinX;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= NeighbourDistance)
                    {
                        for (int j = 0; j < InputNeurons; j++)
                        {
                            //set delta weights and update later
                            //deltaW[x,y,j] = deltaW[x,y,j] + LearnRate * (Input[j] - W[x, y, j]);
                            //v2, present one pattern at a time and update weights now
                            W[x, y, j] += LearnRate * (Input[j] - W[x, y, j]);
                        }
                    }
                }
            }
            
            return ErrorDelta;
        }

        //private void ZeroDeltaWeights()
        //{
        //    for (int y = 0; y < OutputDimension; y++)
        //    {
        //        for (int x = 0; x < OutputDimension; x++)
        //        {
        //            for (int j = 0; j < InputNeurons; j++)
        //            {
        //                deltaW[x, y, j] = 0;
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// Add all delta weights on to weights at the end of a training epoch
        /// </summary>
        //private double UpdateDeltaWeights()
        //{
        //    double Sum = 0;
        //    for (int y = 0; y < OutputDimension; y++)
        //    {
        //        for (int x = 0; x < OutputDimension; x++)
        //        {
        //            for (int j = 0; j < InputNeurons; j++)
        //            {
        //                Sum += Math.Abs(deltaW[x, y, j]);
        //                W[x,y,j]+=deltaW[x, y, j];
        //            }
        //        }
        //    }
        //    return Sum;
        //}

        /// <summary>
        /// Go through the whole training set and sum the mean square errors for every presented pattern.
        /// This number is likely to be very big if there are 2558 datasets times 7201 areas = 18420158;
        /// As an alternative, you could look at the stats of the worst and best case.
        /// Divide this answer datasets*areas to get average error, which is more useful.
        /// POST: places errors into eAll (RMS over all datasets), eMin (RMS best dataset) and eMax (RMS worst dataset)
        /// </summary>
        /// <param name="Matrix"></param>
        /// <returns></returns>
        public double CalculateError(List<double[]> Matrix)
        {
            double e=0;
            eMin = double.MaxValue; eMax = 0;
            for (int i = 0; i < Matrix.Count; i++)
            {
                RunningStat rsx = new RunningStat();
                foreach (double value in Matrix[i]) rsx.Push(value);
                double MeanX = rsx.Mean, SDX = rsx.StandardDeviation;
                if (double.IsNaN(MeanX)||double.IsNaN(SDX)||(SDX==0)) {
                    //System.Diagnostics.Debug.WriteLine("Skipping "+VariableNamesIndex[i]);
                    continue;
                }
                double[] X = new double[InputNeurons];
                for (int j = 0; j < InputNeurons; j++) X[j] = (Matrix[i][j] - MeanX) / SDX;

                Forward(X);
                double Sum=0;
                for (int j = 0; j < InputNeurons; j++) Sum += (W[WinX, WinY, j] - X[j]) * (W[WinX, WinY, j] - X[j]);
                Sum = Math.Sqrt(Sum);
                e += Sum;
                if (Sum < eMin) eMin = Sum;
                if (Sum > eMax) eMax = Sum;
            }
            eAll = e/(Matrix.Count*InputNeurons);
            eMin /= InputNeurons;
            eMax /= InputNeurons;
            return e; //this is the raw error sum
        }

        //Train on all the data and get the result out. Takes in all the data as a matrix of input values
        public void Process(string ImageDirectory)
        {
            //NOTE: need geographic lookup between areas and rows in Matrix is only passed to the output function after
            //the weights have been created - geography not needed for training

            List<double[]> Matrix;
            List<string> VariableNamesIndex;
            BinaryFormatter formatter = new BinaryFormatter();

            //load existing matrix (for speed), copied fron Datastore.ProcessKNearestNeighbourCorrelate
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "matrix.bin"), FileMode.Open))
            {
                Matrix = (List<double[]>)formatter.Deserialize(reader);
            }
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "varnamesindex.bin"), FileMode.Open))
            {
                VariableNamesIndex = (List<string>)formatter.Deserialize(reader);
            }


            //TODO: several times through training set with modification in learning rate and neighbourhood
            //now do the training
            Epoch = 0;
            double e=0;
            double DatasetsAreas = Matrix.Count * InputNeurons;
            do
            {
                double LearnRate=0.001, Distance=0.5;
                //if (Epoch < 2) { LearnRate = 0.85; Distance = 4.0; }
                //else if (Epoch < 4) { LearnRate = 0.5; Distance = 3.0; }
                //else if (Epoch < 6) { LearnRate=0.1; Distance=2.0; }
                //else if (Epoch < 8) { LearnRate = 0.1; Distance = 1.0; }
                //else { LearnRate = 10.0 / (float)Epoch; Distance = 0.5; }
                LearnRate = 1.0-(((double)Epoch + 1.0) / 10000.0);
                if (LearnRate < 0.1) LearnRate = 0.1;
                Distance = 4.0-(((double)Epoch + 1.0) / 1000.0);
                if (Distance < 0.5) Distance = 0.5;

                //ZeroDeltaWeights();

                //for (int i = 0; i <Matrix.Count; i++)
                //{
                int i = trainingRnd.Next(0, Matrix.Count); //pick a random pattern to apply
                    //System.Diagnostics.Debug.WriteLine("Applying: " + VariableNamesIndex[i]);
                    //Normalise input here - sd and mean, same method as correlation and KNN
                    RunningStat rsx = new RunningStat();
                    foreach (double value in Matrix[i]) rsx.Push(value);
                    double MeanX = rsx.Mean, SDX = rsx.StandardDeviation;
                    if (double.IsNaN(MeanX)||double.IsNaN(SDX)||(SDX==0)) {
                        //System.Diagnostics.Debug.WriteLine("Skipping "+VariableNamesIndex[i]);
                        continue;
                    }
                    double[] X = new double[InputNeurons];
                    for (int j = 0; j < InputNeurons; j++) X[j] = (Matrix[i][j] - MeanX) / SDX;

                    //back propagate, sum errors across whole of training set (NOTE: not using this error value)
                    double deltaE = Backward(LearnRate, Distance, X); //LearnRate and Distance here
                    //System.Diagnostics.Debug.WriteLine("e=" + e + " Mean="+MeanX+" SDX="+SDX);
                //}
                //now all the patterns have been presented, add the delta weights onto the weights and calculate the change
                //double deltaSum = UpdateDeltaWeights();

                //periodically present all the patterns and recalculate the error
                if (Epoch % 100 == 0) e = CalculateError(Matrix); //e=total error over all datasets and areas

                if (Epoch%100==0)
                    System.Diagnostics.Debug.WriteLine("Epoch: "+Epoch+" LearnRate="+LearnRate+" Dist="+Distance+" Error: " + e
                        +" eAll: "+eAll+" eMin: "+eMin+" eMax: "+eMax);
                if (Epoch % 1000 == 0) SaveWeights(Path.Combine(ImageDirectory, "kohonen_weights.bin"));

                ++Epoch;
            } while (eAll > 0.001);

            SaveWeights(Path.Combine(ImageDirectory, "kohonen_weights.bin"));

            //now output the results (the weights are maps) - need area keys
            //currently doing this outside function due to areakey problem
        }

        /// <summary>
        /// Write out the weights files as csv files which you can make maps from
        /// </summary>
        /// <param name="AreaKeyLookup"></param>
        /// <param name="Filename"></param>
        public void WriteOutput(List<string> AreaKeyLookup, string Filename)
        {
            //one file per xy neuron
            //for (int y = 0; y < OutputDimension; y++)
            //{
            //    for (int x = 0; x < OutputDimension; x++)
            //    {
            //        using (TextWriter writer = File.CreateText(Path.Combine(OutputDir,"kohonen_"+x+"_"+y+".csv")))
            //        {
            //            writer.WriteLine("areakey,W");
            //            for (int j = 0; j < InputNeurons; j++)
            //            {
            //                writer.WriteLine(AreaKeyLookup[j] + "," + W[x, y, j]);
            //            }
            //        }
            //    }
            //}

            //method 2, write one file, but with multiple columns for the x and y
            using (TextWriter writer = File.CreateText(Filename))
            {
                //header line
                writer.Write("areakey");
                for (int y = 0; y < OutputDimension; y++)
                {
                    for (int x = 0; x < OutputDimension; x++)
                    {
                        writer.Write("," + x + "_" + y);
                    }
                }
                writer.WriteLine();
                
                //now on to the data
                for (int j = 0; j < InputNeurons; j++)
                {
                    writer.Write(AreaKeyLookup[j]);
                    for (int y = 0; y < OutputDimension; y++)
                    {
                        for (int x = 0; x < OutputDimension; x++)
                        {
                            writer.Write("," + W[x,y,j]);
                        }
                    }
                    writer.WriteLine();
                }
            }
        }

        /// <summary>
        /// Take the matrix and variable names lookup from the image directory can apply the classification (using the currently loaded
        /// weights) to all of the training set.
        /// </summary>
        /// <param name="ImageDirectory"></param>
        public void ClassifyAll(string ImageDirectory,string OutFilename)
        {
            List<double[]> Matrix;
            List<string> VariableNamesIndex;
            BinaryFormatter formatter = new BinaryFormatter();

            //load existing matrix (for speed), copied fron Datastore.ProcessKNearestNeighbourCorrelate
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "matrix.bin"), FileMode.Open))
            {
                Matrix = (List<double[]>)formatter.Deserialize(reader);
            }
            using (FileStream reader = new FileStream(Path.Combine(ImageDirectory, "varnamesindex.bin"), FileMode.Open))
            {
                VariableNamesIndex = (List<string>)formatter.Deserialize(reader);
            }

            using (TextWriter writer = File.CreateText(OutFilename))
            {
                writer.Write("j,variable,x,y,arg");
                for (int y=0; y<OutputDimension; y++)
                {
                    for (int x = 0; x < OutputDimension; x++)
                    {
                        writer.Write(","+x+"_"+y);
                    }
                }
                writer.WriteLine();
                for (int i = 0; i < Matrix.Count; i++)
                {
                    //TODO: you could move the normalisation out into a separate function as also in the Backward procedure
                    RunningStat rsx = new RunningStat();
                    foreach (double value in Matrix[i]) rsx.Push(value);
                    double MeanX = rsx.Mean, SDX = rsx.StandardDeviation;
                    if (double.IsNaN(MeanX) || double.IsNaN(SDX) || (SDX == 0))
                    {
                        //System.Diagnostics.Debug.WriteLine("Skipping "+VariableNamesIndex[i]);
                        continue;
                    }
                    double[] X = new double[InputNeurons];
                    for (int j = 0; j < InputNeurons; j++) X[j] = (Matrix[i][j] - MeanX) / SDX;

                    Forward(X);
                    writer.Write(i + "," + VariableNamesIndex[i]+"," + WinX + "," + WinY + ","+WinArg);
                    for (int y = 0; y < OutputDimension; y++)
                    {
                        for (int x = 0; x < OutputDimension; x++)
                        {
                            writer.Write("," + OutputNeurons[x,y]);
                        }
                    }
                    writer.WriteLine();
                }
            }
        }


    }
}
