using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Data;

namespace DatastoreMiner
{   
    /// <summary>
    /// Specialisation of Datastore class to interface with the London Datastore
    /// </summary>
    class LondonDatastore : Datastore
    {
        //The catalogue come from here: http://data.london.gov.uk/catalogue (or more accurately here: http://data.london.gov.uk/datafiles/datastore-catalogue.csv )
        public const string CatalogueFile = "datastore-catalogue4.csv";

        //constructor?

        public LondonDatastore()
        {
            //define field names in LondonDatastore data that we require for processing
            //TitleField = "TITLE";
            //LinkField = "CSV_URL";
            //TagsField = "";
            //DescriptionField = "LONGDESC";
            CSVCatalogue reader = new CSVCatalogue();
            this.Catalogue = reader.ReadCatalogue(Path.Combine(DataRootDir, CatalogueFile));

            //then create a schema to describe what the columns are
            //define field names in LondonDatastore data that we require for processing
            Schema = new DatastoreSchema();
            Schema.AddField("TITLE", SemanticFieldType.Title);
            Schema.AddField("LONGDESC", SemanticFieldType.Description);
            Schema.AddField("CSV_URL", SemanticFieldType.Link);
            
        }
    }
}
