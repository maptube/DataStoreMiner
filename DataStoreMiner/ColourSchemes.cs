using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Drawing;
using System.Data;

//STOLEN from MapTube

namespace MapTube.GIS
{
    //TODO: this is a real mess, the three colour schemes really need to be handled in a coherent way, but so that the
    //user interface can separate sequential, diverging and qualitative. The main obstacle to this is the fact that the
    //colour index numbers for when there are 1,2,3...10 colours used are shared within each group. This means separating
    //into three colour scheme types is a sensible structural choice.
    //Also, it might be better to load the colours from a resource file rather than hard coding them.
    public enum ColourSchemeType { Sequential, Diverging, Qualitative };

    //this is a grouping of colour schemes by ColourSchemeType e.g. Sequential
    //required because each group shares a set of ColourIndexes
    public class ColourSchemeGroup
    {
        public string name;
        public ColourSchemeType type;
        public int[][] ColourIndexes;
        public ColourScheme[] schemes;
        public ColourSchemeGroup(string name, ColourSchemeType type, int[][] ColIndexes, ColourScheme[] schemes)
        {
            this.name = name;
            this.type = type;
            this.ColourIndexes = ColIndexes;
            this.schemes = schemes;
        }
        public ColourScheme GetNamedColourScheme(string Code)
        {
            //return the named ColourScheme if found, otherwise null
            foreach (ColourScheme csh in schemes)
            {
                if (csh.code == Code) return csh;
            }
            return null; //not found
        }
    }

    //This is a named colour scheme i.e. an ordered list of colours given a name and description
    public class ColourScheme
    {
        public string code;
        public string name;
        public string description;
        public Color [] colours;
        public ColourScheme(string name,string code,string description,string strColours)
        {
            //colours is a string of format: r g b, r g b, r g b...
            this.code = code;
            this.name = name; this.description = description;
            string[] fields = strColours.Split(',');
            colours = new Color[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                string[] rgb = fields[i].Trim().Split(' ');
                colours[i] = Color.FromArgb(Convert.ToInt32(rgb[0]), Convert.ToInt32(rgb[1]), Convert.ToInt32(rgb[2]));
            }
        }
    }

    /// <summary>
    /// Summary description for Colours
    /// </summary>
    public class Colours
    {

        //sequential
        ColourScheme YlGn = new ColourScheme(
            "Yellow to Green",
            "YlGn",
            "Light yellow to dark green",
            "255 255 229, 255 255 204, 247 252 185, 217 240 163, 194 230 153, 173 221 142, 120 198 121, 65 171 93, 49 163 84, 35 132 67, 0 104 55, 0 90 50, 0 69 41"
        );
        ColourScheme YlGnBu = new ColourScheme(
            "Yellow to Blue",
            "YlGnBu",
            "Light yellow to green to dark blue",
            "255 255 217, 255 255 204, 237 248 177, 199 233 180, 161 218 180, 127 205 187, 65 182 169, 29 145 192, 44 127 184, 34 96 168, 37 52 148, 12 44 132, 8 29 88"
        );
        ColourScheme GnBu = new ColourScheme(
            "Green to Blue",
            "GnBu",
            "Light green to dark blue",
            "247 252 240, 240 249 232, 224 243 219, 204 235 197, 186 228 188, 168 221 181, 123 204 196, 78 179 211, 67 162 202, 43 140 190, 8 104 172, 8 88 158, 8 64 129"
        );
        ColourScheme BuGn = new ColourScheme(
            "Blue to Green",
            "BuGn",
            "Light blue to dark green",
            "247 252 253, 237 248 251, 229 245 249, 204 236 230, 178 226 226, 153 216 201, 102 194 164, 65 174 118, 44 162 95, 35 139 69, 0 109 44, 0 88 36, 0 68 27"
        );
        ColourScheme PuBuGn = new ColourScheme(
            "Purple, Blue, Green",
            "PuBuGn",
            "Light purple to blue to dark green",
            "255 247 251, 246 239 247, 236 226 240, 208 209 230, 189 201 225, 166 189 219, 116 169 207, 54 144 192, 28 144 153, 2 129 138, 1 108 89, 1 100 80, 1 70 54"
        );
        ColourScheme PuBu = new ColourScheme(
            "Purple to Blue",
            "PuBu",
            "Light purple to dark blue",
            "255 247 251, 241 238 246, 236 231 242, 208 209 230, 189 201 225, 166 189 219, 103 169 207, 54 144 192, 43 140 190, 5 112 176, 4 90 141, 3 78 123, 2 56 88"
        );
        ColourScheme BuPu = new ColourScheme(
            "Blue to Purple",
            "BuPu",
            "Light blue to dark purple",
            "247 252 253, 237 248 251, 224 236 244, 191 211 230, 179 205 227, 158 188 218, 140 150 198, 140 107 177, 136 86 167, 136 65 157, 129 15 124, 110 1 107, 77 0 75"
        );
        ColourScheme RdPu = new ColourScheme(
            "Red to Purple",
            "RdPu",
            "Light red to dark purple",
            "255 247 243, 254 235 226, 253 224 221, 252 197 192, 251 180 185, 250 159 181, 247 104 161, 221 52 151, 197 27 138, 174 1 126, 122 1 119, 122 1 119, 73 0 106"
        );
        ColourScheme PuRd = new ColourScheme(
            "Purple to Red",
            "PuRd",
            "Light purple to dark red",
            "247 244 249, 241 238 246, 231 225 239, 212 185 218, 215 181 216, 201 148 199, 223 101 176, 231 41 138, 221 28 119, 206 18 86, 152 0 67, 145 0 63, 103 0 31"
        );
        ColourScheme OrRd = new ColourScheme(
            "Orange to Red",
            "OrRd",
            "Light orange to dark red",
            "255 247 236, 254 240 217, 254 232 200, 253 212 158, 253 204 138, 253 187 132, 252 141 89, 239 101 72, 227 74 51, 215 48 31, 179 0 0, 153 0 0, 127 0 0"
        );
        ColourScheme YlOrRd = new ColourScheme(
            "Yellow, Orange, Red",
            "YlOrRd",
            "Light yellow to orange to dark red",
            "255 255 204, 255 255 178, 255 237 160, 254 217 118, 254 204 92, 254 178 76, 253 141 60, 252 78 42, 240 59 32, 227 26 28, 189 0 38, 177 0 38, 128 0 38"
        );
        ColourScheme YlOrBr = new ColourScheme(
            "Yellow, Orange, Brown",
            "YlOrBr",
            "Light yellow to orange to dark brown",
            "255 255 229, 255 255 212, 255 247 188, 254 227 145, 254 217 142, 254 196 79, 254 153 41, 236 112 20, 217 95 14, 204 76 2, 153 52 4, 140 45 4, 102 37 6"
        );
        ColourScheme Purples = new ColourScheme(
            "Purples",
            "Purples",
            "Light purple to dark purple",
            "252 251 253, 242 240 247, 239 237 245, 218 218 235, 203 201 226, 188 189 220, 158 154 200, 128 125 186, 117 107 177, 106 81 163, 84 39 143, 74 20 134, 63 0 125"
        );
        ColourScheme Blues = new ColourScheme(
            "Blues",
            "Blues",
            "Light blue to dark blue",
            "247 251 255, 239 243 255, 222 235 247, 198 219 239, 189 215 231, 158 202 225, 107 174 214, 66 146 198, 49 130 189, 33 113 181, 8 81 156, 8 69 148, 8 48 107"
        );
        ColourScheme Greens = new ColourScheme(
            "Greens",
            "Greens",
            "Light green to dark green",
            "247 252 245, 237 248 233, 229 245 224, 199 233 192, 186 228 179, 161 217 155, 116 196 118, 65 171 93, 49 163 84, 35 139 69, 0 109 44, 0 90 50, 0 68 27"
        );
        ColourScheme Oranges = new ColourScheme(
            "Oranges",
            "Oranges",
            "Light orange to dark orange",
            "255 245 235, 254 237 222, 254 230 206, 253 208 162, 253 190 133, 253 174 107, 253 141 60, 241 105 19, 230 85 13, 217 72 1, 166 54 3, 140 45 4, 127 39 4"
        );
        ColourScheme Reds = new ColourScheme(
            "Reds",
            "Reds",
            "Light red to dark red",
            "255 245 240, 254 229 217, 254 224 210, 252 187 161, 252 174 145, 252 146 114, 251 106 74, 239 59 44, 222 45 38, 203 24 29, 165 15 21, 153 0 13, 103 0 13"
        );
        ColourScheme Grays = new ColourScheme(
            "Grays",
            "Grays",
            "Light gray to dark gray",
            "255 255 255, 247 247 247, 240 240 240, 217 217 217, 204 204 204, 189 189 189, 150 150 150, 115 115 115, 99 99 99, 82 82 82, 37 37 37, 37 37 37, 0 0 0"
        );

        //This defines which colours in the scheme are used for palettes with 2,3...9 entries. Number is index into colour array.
        int[][] seqcolindexes =
        {
            new int[] {5,8},
            new int[] {2,5,8},
            new int[] {1,4,6,9},
            new int[] {1,4,6,8,10},
            new int[] {0,2,5,7,10,12},
            new int[] {0,2,5,6,8,10,12},
            new int[] {0,2,3,5,6,9,10,11},
            new int[] {0,2,3,5,6,7,9,10,12} 
        };

        //Diverging
        ColourScheme PuOr = new ColourScheme(
            "Purple Orange",
            "PuOr",
            "Dark orange to light to dark purple",
            "127 59 8, 179 88 6, 230 97 1, 224 130 20, 241 163 64, 253 184 99, 254 224 182, 247 247 247, 216 218 235, 178 171 210, 153 142 195, 128 115 172, 94 60 153, 84 39 136, 45 0 75"
        );
        ColourScheme BrBG = new ColourScheme(
            "Brown to Blue Green",
            "BrBG",
            "dark brown to light to dark blue-green",
            "84 48 5, 140 81 10, 166 97 26, 191 129 45, 216 179 101, 223 194 125, 246 232 195, 245 245 245, 199 234 229, 128 205 193, 90 180 172, 53 151 143, 1 133 113, 1 102 94, 0 60 48"
        );
        ColourScheme PRGn = new ColourScheme(
            "Purple to green",
            "PRGn",
            "dark reddish-purple to light to dark green",
            "64 0 75, 118 42 131, 123 50 148, 153 112 171, 175 141 195, 194 165 207, 231 212 232, 247 247 247, 217 240 211, 166 219 160, 127 191 123, 90 174 97, 0 136 55, 27 120 55, 0 68 27"
        );
        ColourScheme PiYG = new ColourScheme(
            "Magenta to yellow green",
            "PiYG",
            "dark magenta to light to dark yellow-green",
            "142 1 82, 197 27 125, 208 28 139, 222 119 174, 233 163 201, 241 182 218, 253 224 239, 247 247 247, 230 245 208, 184 225 134, 161 215 106, 127 188 65, 77 172 38, 77 146 33, 39 100 25"
        );
        ColourScheme RdBu = new ColourScheme(
            "Red to dark blue",
            "RdBu",
            "dark red to light to dark blue",
            "103 0 31, 178 24 43, 202 0 32, 214 96 77, 239 138 98, 244 165 130, 253 219 199, 247 247 247, 209 229 240, 146 197 222, 103 169 207, 67 147 195, 5 113 176, 33 102 172, 5 48 97"
        );
        ColourScheme RdGy = new ColourScheme(
            "Red to gray",
            "RdGy",
            "dark red to light to dark gray",
            "103 0 31, 178 24 43, 202 0 32, 214 96 77, 239 138 98, 244 165 130, 253 219 199, 255 255 255, 224 224 224, 186 186 186, 153 153 153, 135 135 135, 64 64 64, 77 77 77, 26 26 26"
        );
        ColourScheme RdYlBu = new ColourScheme(
            "Red to yellow to blue",
            "RdYlBu",
            "dark red to light yellow to dark blue",
            "165 0 38, 215 48 39, 215 25 28, 244 109 67, 252 141 89, 253 174 97, 254 224 144, 255 255 191, 224 243 248, 171 217 233, 145 191 219, 116 173 209, 44 123 182, 69 117 180, 49 54 149"
        );
        ColourScheme Spectral = new ColourScheme(
            "Spectral",
            "Spectral",
            "dark red, orange, light yellow, green, dark blue",
            "158 1 66, 213 62 79, 215 25 28, 244 109 67, 252 141 89, 253 174 97, 254 224 139, 255 255 191, 230 245 152, 171 221 164, 153 213 148, 102 194 165, 43 131 186, 50 136 189, 94 79 162"
        );
        ColourScheme RdYlGn = new ColourScheme(
            "Red yellow green",
            "RdYlGn",
            "dark red, orange, light yellow, yellow-green, dark green",
            "165 0 38, 215 48 39, 215 25 28, 244 109 67, 252 141 89, 253 174 97, 254 224 139, 255 255 191, 217 239 139, 166 217 106, 145 207 96, 102 189 99, 26 150 65, 26 152 80, 0 104 55"
        );
        //This defines which colours in the scheme are used for palettes with 2,3...11 entries. Number is index into colour array.
        int[][] divcolindexes =
        {
            new int[] {7,10},
            new int[] {4,7,10},
            new int[] {2,5,9,12},
            new int[] {3,5,7,9,12},
            new int[] {1,4,6,8,10,13},
            new int[] {1,4,6,7,8,10,13},
            new int[] {1,3,5,6,8,9,11,13},
            new int[] {1,3,5,6,7,8,9,11,13},
            new int[] {0,1,3,5,6,8,9,11,13,14},
            new int[] {0,1,3,5,6,7,8,9,11,13,14}
        };

        //Qualitative
        ColourScheme Set1 = new ColourScheme(
            "Set1",
            "Set1",
            "includes bold, readily names, basic colours (such as red, green, blue)",
            "228 26 28, 55 126 184, 77 175 74, 152 78 163, 255 127 0, 255 255 51, 166 86 40, 247 129 191, 153 153 153"
        );
        ColourScheme Pastel1 = new ColourScheme(
            "Pastel1",
            "Pastel1",
            "lighter version of Set 1",
            "251 180 174, 179 205 227, 204 235 197, 222 203 228, 254 217 166, 255 255 204, 229 216 189, 253 218 236, 242 242 242"
        );
        ColourScheme Set2 = new ColourScheme(
            "Set2",
            "Set2",
            "includes mostly mixture colors (such as blue-green, red-orange)",
            "102 194 165, 252 141 98, 141 160 203, 231 138 195, 166 216 84, 255 217 47, 229 196 148, 179 179 179"
        );
        ColourScheme Pastel2 = new ColourScheme(
            "Pastel2",
            "Pastel2",
            "lighter version of Set 2",
            "179 226 205, 253 205 172, 203 213 232, 244 202 228, 230 245 201, 255 242 174, 241 226 204, 204 204 204"
        );
        ColourScheme Dark2 = new ColourScheme(
            "Dark2",
            "Dark2",
            "darker version of Set 2",
            "27 158 119, 217 95 2, 117 112 179, 231 41 138, 102 166 30, 230 171 2, 166 118 29, 102 102 102"
        );
        ColourScheme Set3 = new ColourScheme(
            "Set3",
            "Set3",
            "median saturation set with more lightness variation and more classes than Set 1 or 2",
            "141 211 199, 255 255 179, 190 186 218, 251 128 114, 128 177 211, 253 180 98, 179 222 105, 252 205 229, 217 217 217, 188 128 189, 204 235 197, 255 237 111"
        );
        ColourScheme Paired = new ColourScheme(
            "Paired",
            "Paired",
            "light/dark pairs for nameable hues",
            "166 206 227, 31 120 180, 178 223 138, 51 160 44, 251 154 153, 227 26 28, 253 191 111, 255 127 0, 202 178 214, 106 61 154, 255 255 153, 177 89 40"
        );
        ColourScheme Accents = new ColourScheme(
            "Accents",
            "Accents",
            "includes lightness and saturation extremes to accent small or important areas",
            "127 201 127, 190 174 212, 253 192 134, 255 255 153, 56 108 176, 240 2 127, 191 91 23, 102 102 102"
        );
        //This defines which colours in the scheme are used for palettes with 2,3...12 entries. Number is index into colour array.
        int[][] qualcolindexes =
        {
            new int[] {0,1},
            new int[] {0,1,2},
            new int[] {0,1,2,3},
            new int[] {0,1,2,3,4},
            new int[] {0,1,2,3,4,5},
            new int[] {0,1,2,3,4,5,6},
            new int[] {0,1,2,3,4,5,6,7},
            new int[] {0,1,2,3,4,5,6,7,8},
            new int[] {0,1,2,3,4,5,6,7,8,9},
            new int[] {0,1,2,3,4,5,6,7,8,9,10},
            new int[] {0,1,2,3,4,5,6,7,8,9,10,11}
        };

//end of colour sequence definitions


        public Colours()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        /// <summary>
        /// Make the sequential group of colours from the defined constants and return a ColourSchemeGroup structure
        /// </summary>
        /// <returns></returns>
        public ColourSchemeGroup GetSequential()
        {
            ColourSchemeGroup sequential = new ColourSchemeGroup(
                "Sequential",
                ColourSchemeType.Sequential,
                seqcolindexes,
                new ColourScheme[] { YlGn, YlGnBu, GnBu, BuGn, PuBuGn, PuBu, BuPu, RdPu, PuRd, OrRd, YlOrRd, YlOrBr,
                    Purples, Blues, Greens, Oranges, Reds, Grays });
            return sequential;
        }

        /// <summary>
        /// Make the diverging group of colours from the defined constants and return a ColourSchemeGroup structure
        /// </summary>
        /// <returns></returns>
        public ColourSchemeGroup GetDiverging()
        {
            ColourSchemeGroup diverging = new ColourSchemeGroup(
                "Diverging",
                ColourSchemeType.Diverging,
                divcolindexes,
                new ColourScheme[] { PuOr, BrBG, PRGn, PiYG, RdBu, RdGy, RdYlBu, Spectral, RdYlGn });
            return diverging;
        }

        /// <summary>
        /// Make the qualitative group of colours from the defined constants and return a ColourSchemeGroup structure
        /// </summary>
        /// <returns></returns>
        public ColourSchemeGroup GetQualitative()
        {
            ColourSchemeGroup qualitative = new ColourSchemeGroup(
                "Qualitative",
                ColourSchemeType.Qualitative,
                qualcolindexes,
                new ColourScheme[] { Set1, Pastel1, Set2, Pastel2, Dark2, Set3, Paired, Accents });
            return qualitative;
        }

        /// <summary>
        /// Put all the colour schemes from a colour scheme group into a DataSet and return it
        /// </summary>
        /// <param name="csg">The colour scheme group to make into a dataset</param>
        /// <returns></returns>
        public DataSet GetColourSchemeGroupDS(ColourSchemeGroup csg)
        {
            DataSet ds = new DataSet(csg.name);

            //build a master table containing all the colour scheme names
            DataTable namesDT = new DataTable("SchemeNames");
            ds.Tables.Add(namesDT);
            namesDT.Columns.Add("Name");
            namesDT.Columns.Add("Code");
            foreach (ColourScheme scheme in csg.schemes)
            {
                namesDT.Rows.Add(scheme.name,scheme.code);
            }

            //now make a big list of the colours for every colour scheme joined to the SchemeNames table on the name column
            DataTable coloursDT = new DataTable("Colours");
            ds.Tables.Add(coloursDT);
            coloursDT.Columns.Add("Name", typeof(string));
            coloursDT.Columns.Add("Code", typeof(string));
            coloursDT.Columns.Add("Colour", typeof(Color));
            foreach (ColourScheme scheme in csg.schemes)
            {
                //Sometimes there are a maximum of 9 colours in a scheme, but 13 RGB triples defined because not every
                //sequence (e.g. 2 coles, 3 cols... 9 cols) uses the same RGB set. In the 9 colour sequence not all colours
                //are used. In the second case, some schemes within a colour scheme group could have fewer RGB triples than
                //there are indexes in the index table i.e. some spectral schemes only have a limited number of colours.
                //To cope with this, we have to work out whether the ColourIndex array or the scheme RGB triples are the minimum
                //in order to get the correct number of colours.
                //NOTE: ColourIndexes[0] is the two colour set, hence -2 to get the number of colours-1 and the correct index.
                int numcols = scheme.colours.Length-2;
                if (numcols >= csg.ColourIndexes.Length) numcols = csg.ColourIndexes.Length - 1;
                //int [] indexes = csg.ColourIndexes[csg.ColourIndexes.Length-1];
                int [] indexes = csg.ColourIndexes[numcols];
                foreach (int i in indexes)
                {
                    coloursDT.Rows.Add(scheme.name,scheme.code,scheme.colours[i]);
                }
            }

            //Create the relation between the Themes and Projects tables which are joined on the ThemeID fields in both
            ds.Relations.Add("rel_schemecolours", ds.Tables["SchemeNames"].Columns["Code"], ds.Tables["Colours"].Columns["Code"]);

            return ds;
        }

        /// <summary>
        /// Helper function for getting a named colour scheme from any of the sequential, diverging or qualitative groups.
        /// </summary>
        /// <param name="Code">The named colour scheme to find</param>
        /// <returns>A colour scheme or null</returns>
        public static ColourScheme FindNamedColourScheme(string Code)
        {
            ColourScheme Result;
            ColourSchemeGroup Group;
            FindNamedColourScheme(Code, out Result, out Group);
            return Result;
        }

        /// <summary>
        /// Get a set of named colours for a specific number of thresholds. Used to make the colour scale from.
        /// </summary>
        /// <param name="Code">The named ColourScheme</param>
        /// <param name="NumColours">The number of colours required</param>
        /// <returns>A set of colours, padded with black if NumColours exceeds the number in the scheme</returns>
        public static Color[] FindNamedColours(string Code, int NumColours)
        {
            Color[] cols = new Color[NumColours];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.Black;

            ColourScheme csh;
            ColourSchemeGroup Group;
            if (FindNamedColourScheme(Code, out csh, out Group))
            {
                //cope with colours for a scheme starting at 2 and what happens if you request more colours than there are 
                int ColIdx = NumColours - 2;
                if (ColIdx < 0) ColIdx = 0;
                else if (ColIdx >= Group.ColourIndexes.Length) ColIdx = Group.ColourIndexes.Length - 1;
                //old code: runs off end of array
                //for (int i = 0; i < Group.ColourIndexes[ColIdx].Length; i++)
                //{
                //    cols[i]=csh.colours[Group.ColourIndexes[ColIdx][i]];
                //}
                int ActualColours = Math.Min(NumColours, Group.ColourIndexes[ColIdx].Length); //don't run off end of colour array
                for (int i = 0; i < ActualColours; i++)
                {
                    cols[i] = csh.colours[Group.ColourIndexes[ColIdx][i]];
                }
            }
            return cols;
        }

        /// <summary>
        /// Helper function to find a given number of colours for a named colour scheme.
        /// </summary>
        /// <param name="code">The named colour scheme to find</param>
        /// <param name="csh">The colour scheme</param>
        /// <param name="Group">The group the colour scheme belongs to e.g. Sequential</param>
        /// <returns>true if the named colour scheme was found</returns>
        public static bool FindNamedColourScheme(string code, out ColourScheme csh, out ColourSchemeGroup Group)
        {
            csh = null; Group = null;
            Colours cols = new Colours();
            Group = cols.GetSequential();
            csh = Group.GetNamedColourScheme(code);
            if (csh == null)
            {
                Group = cols.GetDiverging();
                csh = Group.GetNamedColourScheme(code);
                if (csh == null)
                {
                    Group = cols.GetQualitative();
                    csh = Group.GetNamedColourScheme(code);
                }
            }
            return csh != null;
        }

    }
}