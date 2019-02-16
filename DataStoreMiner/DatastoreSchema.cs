using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatastoreMiner
{
    /// <summary>
    /// Semantic type of columns in data. Basically defines what we do with the columns in the rest of the program.
    /// Ignore: don't bother, this column has no useful information
    /// Text: some sort of undefined plain text field (meta-data)
    /// Link: URI link to the data
    /// Title: plain text field used as the human-readable title for this dataset
    /// Description: plain text description of the data contained in this dataset
    /// Tags: list of tag keywords, often used to link maps together in a graph based on matched keywords
    /// UniqueKey: unique field from manifest which can be used to identify a particular dataset
    /// </summary>
    public enum SemanticFieldType { Ignore, Text, Link, Title, Description, Tags, UniqueKey };

    /// <summary>
    /// Description of the catalogue of a datastore and what all the columns are used for
    /// </summary>
    public class DatastoreSchema
    {

        public class SemanticField
        {
            public string Name;
            public SemanticFieldType FieldType;
        }

        public List<SemanticField> Fields;

        #region properties

        private string ReturnFirstFieldOfType(SemanticFieldType sft)
        {
            foreach (SemanticField sf in Fields)
            {
                if (sf.FieldType == sft) return sf.Name;
            }
            return null;
        }

        //TODO: these Link, Title, Description field methods should probably return a list of matching fields, not just the first,
        //but that would complicate a whole lot of things, so just assume the important fields only happen once.
        //Don't really need following properties, but they serve as a convenience

        public string LinkField
        {
            get
            {
                return ReturnFirstFieldOfType(SemanticFieldType.Link);
            }
        }

        public string TitleField
        {
            get
            {
                return ReturnFirstFieldOfType(SemanticFieldType.Title);
            }
        }

        public string DescriptionField
        {
            get
            {
                return ReturnFirstFieldOfType(SemanticFieldType.Description);
            }
        }

        public string TagsField
        {
            get
            {
                return ReturnFirstFieldOfType(SemanticFieldType.Tags);
            }
        }

        public string UniqueKeyField
        {
            get
            {
                return ReturnFirstFieldOfType(SemanticFieldType.UniqueKey);
            }
        }

        #endregion properties

        public DatastoreSchema()
        {
            Fields = new List<SemanticField>();
        }

        public void AddField(string Name, SemanticFieldType FieldType)
        {
            SemanticField sf = new SemanticField();
            sf.Name = Name;
            sf.FieldType = FieldType;
            Fields.Add(sf);
        }

    }
}
