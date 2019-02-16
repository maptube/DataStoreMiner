using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.IO;

//TODO: could do with changing the namespace to an NLP one?
namespace DatastoreMiner.NLP
{
    /// <summary>
    /// Tuple to hold the original word characters and the stem word that this results in.
    /// The default comparator uses the Stem Word rather than the orginal word.
    /// </summary>
    public class StemWordTuple : IComparable
    {
        public string Word;
        public string Stem;
        public int CompareTo(object o)
        {
            return Stem.CompareTo(o);
        }
        //need to override ==, !=, Equals and GetHashCode for Tuple matching in hashes to work (Dictionary)
        public static bool operator ==(StemWordTuple A,StemWordTuple B)
        {
            return A.Stem==B.Stem;
        }
        public static bool operator !=(StemWordTuple A,StemWordTuple B)
        {
            return A.Stem!=B.Stem;
        }
        public override bool Equals(object o)
        {
            return this.Stem==((StemWordTuple)o).Stem;
        }
        public override int GetHashCode()
        {
            return Stem.GetHashCode();
        }
        public override string ToString()
        {
            return Word + "(" + Stem + ")";
        }
    }

    /// <summary>
    /// Contains a stem word tuple and a count indicating frequency - useful for keyword processing
    /// </summary>
    public class StemWordCount
    {
        public StemWordTuple StemWord;
        public float Count;
    }

    /// <summary>
    /// Functions for processing Natural Language, for example extracting meaningful keywords from blocks of strings
    /// </summary>
    public class NaturalLanguage
    {
        private StemmerInterface Stemmer = new PorterStemmer();

	    public NaturalLanguage()
	    {
		    //
		    // TODO: Add constructor logic here
		    //
        }

        /// <summary>
        /// Use the Word Stemmer interface to generate a stem from a single word.
        /// This uses the Porter word stemmer algorithm.
        /// </summary>
        /// <param name="Word">The word to stem</param>
        /// <returns>The stem of the word</returns>
        public string StemWord(string Word)
        {
            return Stemmer.stemTerm(Word);
        }

        /// <summary>
        /// Create a stem word tuple from the raw word which we create a stem word from and store both.
        /// </summary>
        /// <param name="Word"></param>
        /// <returns></returns>
        public StemWordTuple CreateStemWord(string Word)
        {
            StemWordTuple sw = new StemWordTuple();
            sw.Word = Word;
            sw.Stem = StemWord(Word);
            return sw;
        }

        /// <summary>
        /// Strip HTML tags out of a fragment of text. Used for Gov Datastore descriptions which begin and end with p /p
        /// TODO: this isn't going to be completely rigorous
        /// TODO: DEFINITELY MUST handle &nbsp; etc...
        /// At the moment it doesn't understand CDATA sections
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string StripHtml(string Text)
        {
            if (string.IsNullOrEmpty(Text)) return Text;

            StringBuilder builder = new StringBuilder();
            bool InTag = false;
            for (int i = 0; i < Text.Length; i++)
            {
                char ch = Text[i];
                if (ch == '<') InTag = true;
                else if (InTag)
                {
                    if (ch == '>') InTag = false;
                }
                else
                {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Turn a block of text into a list of words (lowercase with no punctuation).
        /// No words less than 3 characters are allowed.
        /// </summary>
        /// <param name="Text"></param>
        /// <returns></returns>
        public static string[] SplitWords(string Text)
        {
            //TODO: really need to process out http addresses before processing the text as English
            //split words on space, comma, dot or newline and process
            if (string.IsNullOrEmpty(Text)) return new string [] { };
            
            string[] Words = Text.Split(new char[] { ' ', ',', '.', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> NewWords = new List<string>();
            for (int i = 0; i < Words.Length; i++)
            {
                StringBuilder builder = new StringBuilder();
                for (int j = 0; j < Words[i].Length; j++)
                {
                    char ch = Words[i][j];
                    if ((ch >= 'a' && ch <= 'z') | (ch == '-')) //lowercase characters of hypen retained
                    {
                        builder.Append(ch);
                    }
                    else if (ch >= 'A' && ch <= 'Z') //uppercase characters converted to lower
                    {
                        builder.Append(Char.ToLower(ch));
                    }
                    //any other characters removed (numbers or punctuation)
                }
                //TODO: could remove leading and trailing non-alpha characters here (=- gets through on some of the words) 
                Words[i] = builder.ToString().Trim();
                if (!string.IsNullOrEmpty(Words[i]) && (Words[i].Length > 2)) NewWords.Add(Words[i]);
            }
            return NewWords.ToArray();
        }

        /// <summary>
        /// Load the stop words list into a hash set
        /// </summary>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public static HashSet<string> LoadStopWords(string Filename)
        {
            HashSet<string> Result = new HashSet<string>();
            using (TextReader reader = File.OpenText(Filename))
            {
                string Line;
                while ((Line = reader.ReadLine()) != null)
                {
                    Result.Add(Line);
                }
            }
            return Result;
        }
	}
}