using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

using DatastoreMiner.NLP;

namespace DatastoreMiner
{
    public class KeywordProcessor
    {
        private HashSet<string> StopWordsList;

        public KeywordProcessor(string StopWordsFilename)
        {
            //load the stop words into a hash
            StopWordsList = NaturalLanguage.LoadStopWords(StopWordsFilename);
        }

        /// <summary>
        /// Use the official test extraction interface.
        /// THIS IS THE BEST METHOD
        /// </summary>
        /// <param name="Filename"></param>
        public string GetPdfFileText(string SrcFilename)
        {
            StringBuilder builder = new StringBuilder();
            PdfReader reader = new PdfReader(SrcFilename);
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                ITextExtractionStrategy its = new iTextSharp.text.pdf.parser.SimpleTextExtractionStrategy();
                //ITextExtractionStrategy its = new CSVTextExtractionStrategy();
                string PageText = PdfTextExtractor.GetTextFromPage(reader, page, its);
                //PageCSVText = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(PageCSVText)));
                //Byte[] b = Encoding.UTF8.GetBytes(PageCSVText);
                //PageCSVText = Encoding.UTF8.GetString(Encoding.ASCII.GetBytes(PageCSVText));
                //System.Diagnostics.Debug.WriteLine(PageText);
                builder.AppendLine(PageText);
            }
            reader.Close();

            return builder.ToString();
        }

        /// <summary>
        /// Take a block of text containing words, separate anything with spaces in between to make separate words and build
        /// a count of unique words against number of occurances.
        /// Filters out words 3 characters or less and converts everything to lowercase.
        /// I'm using floats rather than ints for the count as everything ends up as weights in the end.
        /// </summary>
        /// <param name="Text">The block of text to process</param>
        /// <returns>A dictionary of word counts where all the words are lowercase and >3 characters</returns>
        public Dictionary<string, float> TextToHistogram(string Text)
        {
            Dictionary<string, float> hist = new Dictionary<string, float>();
            string[] words = NaturalLanguage.SplitWords(Text);
            foreach (string word in words)
            {
                if (hist.ContainsKey(word)) hist[word] += 1;
                else hist.Add(word, 1);
            }
            return hist;
        }

        /// <summary>
        /// Apply the stem words algorithm to all the words in a histogram and return a new one with correct counts.
        /// I'm using floats rather than ints for the counts as everything ends up as weights in the end.
        /// </summary>
        /// <param name="Hist"></param>
        /// <returns></returns>
        public Dictionary<string, float> StemHistogram(Dictionary<string, float> Hist)
        {
            NaturalLanguage nlp = new NaturalLanguage();
            Dictionary<string, float> StemHist = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> KVP in Hist)
            {
                string StemWord = nlp.StemWord(KVP.Key);
                if (StemHist.ContainsKey(StemWord)) StemHist[StemWord] += KVP.Value;
                else StemHist.Add(StemWord, KVP.Value);
            }
            return StemHist;
        }

        /// <summary>
        /// Normalise the word histogram data
        /// </summary>
        /// <param name="Hist"></param>
        /// <returns></returns>
        public Dictionary<string, float> NormaliseBagOfWords(Dictionary<string, float> Hist)
        {
            Dictionary<string, float> NormHist = new Dictionary<string, float>();
            //normalise by taking square root of the sum of squares
            float Sum = 0;
            foreach (float Count in Hist.Values) Sum += Count*Count;
            float Mag = (float)Math.Sqrt(Sum);
            foreach (KeyValuePair<string, float> KVP in Hist)
            {
                NormHist.Add(KVP.Key, ((float)KVP.Value) / Mag);
            }
            return NormHist;
        }

        /// <summary>
        /// Calculate the number of documents that each word in the Vector Space Model occurs in and return a new histogram
        /// of the VSM with the weights all recalculated.
        /// </summary>
        /// <param name="VSM"></param>
        /// <returns></returns>
        public Dictionary<string, float> [] TermFrequencyInverseDocumentFrequency(Dictionary<string, float> [] VSM)
        {
            //Following pp244 of Principles of Data Mining by Max Bramer, Springer UTiCS.
            //We take the number of times a word occurs in the document, f(tj)
            //and the number of documents tj occurs in, nj.
            //n is the total number of documents.
            //Then inverse document frequency is log2(n/nj).
            //TFIDF=f(tf) * log2(n/nj)

            //First step, find all the unique words in the VSM and count the number of documents they appear in.
            //OK, this is just like the other histograms, but on an array of histograms as the input.
            Dictionary<string, float> DocHist = new Dictionary<string, float>(); //this contains the nj values
            for (int i = 0; i < VSM.Length; i++)
            {
                foreach (string Word in VSM[i].Keys)
                {
                    //NOTE: this only works because we can guarantee that VSM[i] only contains unqiue words. Otherwise
                    //you might be adding the same word twice when yo only want to add one for it being in document i.
                    if (DocHist.ContainsKey(Word)) DocHist[Word] += 1;
                    else DocHist.Add(Word, 1);
                }
            }
            //OK, DocHist contains the count of how many documents each word appears in, nj

            //Now modify the VSM to produce a new one using the TFDIF formula
            float n = VSM.Length; //number of documents
            Dictionary<string, float>[] TFIDIF = new Dictionary<string, float>[VSM.Length];
            for (int i = 0; i < VSM.Length; i++)
            {
                Dictionary<string, float> D = new Dictionary<string, float>();
                foreach (KeyValuePair<string, float> KVP in VSM[i])
                {
                    string tj = KVP.Key; //term j (word)
                    float ftj = KVP.Value; //frequency of tj in document
                    float nj = DocHist[tj]; //number of documents tj appears in
                    D.Add(KVP.Key,ftj*(float)(Math.Log(n/nj)/Math.Log(2))); //Log2(n/nj)
                }
                TFIDIF[i] = D;
            }
            return TFIDIF;
        }

        /// <summary>
        /// Build the correlation matrix for every combination in the catalogue.
        /// Requires a list of pdf files matching the data files.
        /// </summary>
        /// <param name="Catalogue">The DataTable used by the main Datastore class for its catalogue</param>
        /// <param name="Schema">And the Schema is the Datastore.Schema property</param>
        public void GenerateTextCorrelation(DataTable Catalogue, DatastoreSchema Schema)
        {
            //TODO: get the hardcoded directories out!
            //KeywordProcessor kp = new KeywordProcessor(@"..\..\..\Data\glasgow_stop_words_mod.txt");
            //string PdfText = kp.GetPdfFileText(@"C:\richard\wxtemp\Datastores\CensusMetaData\ks101ew.pdf");
            //Dictionary<string, int> WordTable = kp.TextToHistogram(PdfText);
            //Dictionary<string, int> StemWordTable = kp.StemHistogram(WordTable);
            //KeywordProcessor.DebugPrintWords(StemWordTable);

            
            int TitleColIdx = Catalogue.Columns.IndexOf(Schema.TitleField);
            int LinkColIdx = Catalogue.Columns.IndexOf(Schema.LinkField);
            int UniqueKeyColIdx = Catalogue.Columns.IndexOf(Schema.UniqueKeyField);

            //We're going to create a vector space model (VSM) of the histograms for every document in
            //the set. This will enable the number of documents that each word appears in to be calculated
            //for the Term Frequency Inverse Document Frequency (TFIDF) method (Principles of Data Mining
            //by Max Bramer, pp244).
            int N = Catalogue.Rows.Count;
            Dictionary<string, float> [] VSM = new Dictionary<string, float>[N];

            //load all the documents and create histograms
            for (int i = 0; i < N; i++)
            {
                DataRow Row_i = Catalogue.Rows[i];
                string Title_i = Row_i[TitleColIdx] as string;
                //string DataLink_i = Row_i[LinkColIdx] as string;
                string UniqueKey_i = Row_i[UniqueKeyColIdx] as string;

                //extract the data from the pdf for the i table
                string PdfText_i = GetPdfFileText(@"C:\richard\wxtemp\Datastores\CensusMetaData\" + UniqueKey_i + ".pdf");
                Dictionary<string, float> WordTable_i = TextToHistogram(PdfText_i);
                Dictionary<string, float> StemWordTable_i = StemHistogram(WordTable_i);
                //Dictionary<string, float> NormStemWordTable_i = NormaliseBagOfWords(StemWordTable_i);
                VSM[i] = StemWordTable_i;
            }
            //OK, that's the vector space model for all the tables, now do the TFIDF
            Dictionary<string, float>[] TFIDF = TermFrequencyInverseDocumentFrequency(VSM);
            //great, now I've got two copies of all the documents in memory! Lucky they're really quite small.
            //Now normalise all the weights
            for (int i = 0; i < TFIDF.Length; i++)
            {
                TFIDF[i] = NormaliseBagOfWords(TFIDF[i]);
            }

            //and finally, we have all the vectors we need to do the correlation...

            //do for every i, for every j and compute products of matching TFIDF stem words
            for (int i = 0; i < N; i++)
            {
                DataRow Row_i = Catalogue.Rows[i];
                string Title_i = Row_i[TitleColIdx] as string;
                //string DataLink_i = Row_i[LinkColIdx] as string;
                string UniqueKey_i = Row_i[UniqueKeyColIdx] as string;

                for (int j = 0; j < N; j++) //NOTE: could do j=i..N, but want a double check on results being symmetric
                {
                    DataRow Row_j = Catalogue.Rows[j];
                    string Title_j = Row_j[TitleColIdx] as string;
                    //string DataLink_j = Row_j[LinkColIdx] as string;
                    string UniqueKey_j = Row_j[UniqueKeyColIdx] as string;

                    //and finally work out the (inverse) distance i.e. dot product
                    float L = 0;
                    foreach (KeyValuePair<string,float> KVP in TFIDF[i])
                    {
                        if (TFIDF[j].ContainsKey(KVP.Key))
                        {
                            L += KVP.Value * TFIDF[j][KVP.Key]; //i*j from matching keywords 
                        }
                    }
                    //NOTE: Distance would be 1-L at this point, but we want L
                    //write out data
                    System.Diagnostics.Debug.WriteLine(L+","+i + "," + j + "," + UniqueKey_i + "," + UniqueKey_j);
                }
            }
        }

        #region debug

        public static void DebugPrintWords(Dictionary<string, int> Histogram)
        {
            foreach (KeyValuePair<string, int> KVP in Histogram)
            {
                System.Diagnostics.Debug.WriteLine(KVP.Key + "," + KVP.Value);
            }
        }

        #endregion debug
    }
}
