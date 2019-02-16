using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;

namespace DatastoreMiner
{
    //http://catalogue.data.gov.uk/dump/
    //http://catalogue.data.gov.uk/dump/data.gov.uk-ckan-meta-data-2012-05-07.csv.zip
    //Now finding this at the following link:
    //http://data.gov.uk/data/dumps/archive/
    //April 2016: Now here: https://data.gov.uk/dataset/data_gov_uk-datasets

    //need to look at this: https://data.gov.uk/data/metadata-api-docs
    //this gets you a list of packages using the CKAN API, where a package is the name of the data (that's over 26,000 data packages!)
    //http://data.gov.uk/api/action/package_list
    //I guess you then have to query each one individually. The datasets doc above actually gives you more information, although not as much as the package page.
    //http://data.gov.uk/api/action/package_show?id=vehicle_licence_data
    //Not necessarily, resource.csv contains additional information which includes data format (i.e. shapefile)

    //TODO: the best option is to combine the datasets and resources files into a composite catalogue as datasets contains the display data and resources contains the format and url

    /// <summary>
    /// Specialisation of the Datastore class to interface with the Government Datastore
    /// </summary>
    class GovDatastore : Datastore
    {
        //public const string CatalogueFile = "data.gov.uk-ckan-meta-data-2012-05-07.csv";
        //THIS ONE //public const string CatalogueFile = "data.gov.uk-ckan-meta-data-2012-05-07-cropped.csv";
        //public const string CatalogueFile = "short-data.gov.uk-ckan-meta-data-2012-05-07.csv";
        //public const string CatalogueFile = "data.gov.uk-ckan-meta-data-2012-07-15.csv";
        public const string CatalogueFile = "data-gov-uk/datasets.csv";
        public const string CatResourcesFile = "data-gov-uk/resources.csv";

        public DataTable ResourcesDT;

        public GovDatastore()
        {
            //define field names in GovDatastore data that we require for processing
            //TitleField = "title";
            //LinkField = "resource-0-url";
            //TagsField = "tags";
            //DescriptionField = "notes_rendered";
            CSVCatalogue reader = new CSVCatalogue();
            reader.LineEndings = "\r"; //override line endings as this catalogue file only uses a CR
            this.Catalogue = reader.ReadCatalogue(Path.Combine(DataRootDir, CatalogueFile));
            this.ResourcesDT = reader.ReadCatalogue(Path.Combine(DataRootDir, CatResourcesFile));
            //datasets has: Name,Title,URL,Organization,Top level organisation,License,Published,NII,Location,Import source,Author,Geographic Coverage,Isopen,License,License Id,Maintainer,Mandate,Metadata Created,Metadata Modified,Notes,Odi Certificate,ODI Certificate URL,Tags,Temporal Coverage From,Temporal Coverage To,Primary Theme,Secondary Themes,Update Frequency,Version
            //resources has: Dataset Name,URL,Format,Description,Resource ID,Position,Date,Organization,Top level organization
            //so join on Name and Dataset Name

            //todo: this doesn't work as the dataset name in the resources file isn't unique - it contains multiple entries for all the resources attached to a dataset.
            //this means that you're going to have to handle two tables and merge the descriptions together somehow.

            //TODO: none of this works yet
            /*DataColumn DatasetNameCol = resource.Columns["Dataset Name"];
            resource.PrimaryKey = new DataColumn[] { DatasetNameCol };
            //create the new columns in catalogue
            //foreach (DataColumn col in resource.Columns)
            //{
            //    if (col.ColumnName == "URL") this.Catalogue.Columns.Add("URL2"); //there's already one in the catalogue csv file
            //    if (col.ColumnName != "Dataset Name") this.Catalogue.Columns.Add(col.ColumnName, typeof(string));
            //}
            //Manually add columns because of the duplicates
            this.Catalogue.Columns.Add("URL2", typeof(string));
            this.Catalogue.Columns.Add("Format", typeof(string));
            this.Catalogue.Columns.Add("Description", typeof(string));
            this.Catalogue.Columns.Add("Resource ID", typeof(string));
            this.Catalogue.Columns.Add("Position", typeof(string));
            this.Catalogue.Columns.Add("Date", typeof(string));
            //now add elements to row, joining in name and Dataset Namerows
            foreach (DataRow row in this.Catalogue.Rows)
            {
                string DatasetName = row["Name"] as string;
                DataRow ResRow = resource.Rows.Find(DatasetName);
                if (ResRow == null)
                {
                    System.Diagnostics.Debug.WriteLine("Error: resource " + DatasetName + " not found in catalogue");
                }
                else
                {
                    row["URL2"] = ResRow["URL"];
                    row["Format"] = ResRow["Format"];
                    row["Description"] = ResRow["Description"];
                    row["Resource ID"] = ResRow["Resource ID"];
                    row["Position"] = ResRow["Position"];
                    row["Date"] = ResRow["Date"];
                }
            }*/

            //resource-0-format is CSV (also look at RDF etc)
            //also note bbox-east-long, bbox-north-lat, bbox-south-lat, bbox-west-long, spatial-reference-system and spatial contains a polygon box

            //then create a schema to describe what the columns are
            //define field names in GovDatastore data that we require for processing
            //2012 schema
            //Schema = new DatastoreSchema();
            //Schema.AddField("title", SemanticFieldType.Title);
            //Schema.AddField("notes_rendered", SemanticFieldType.Description);
            //Schema.AddField("resource-0-url", SemanticFieldType.Link);
            //Schema.AddField("tags", SemanticFieldType.Tags);

            //2016 schema
            //as of 4 April 2016, the data now looks like this:
            //Name,Title,URL,Organization,Top level organisation,License,Published,NII,Location,Import source,Author,Geographic Coverage,Isopen,License,License Id,Maintainer,Mandate,Metadata Created,Metadata Modified,Notes,Odi Certificate,ODI Certificate URL,Tags,Temporal Coverage From,Temporal Coverage To,Primary Theme,Secondary Themes,Update Frequency,Version
            Schema = new DatastoreSchema();
            Schema.AddField("Title", SemanticFieldType.Title);
            //Schema.AddField("Notes", SemanticFieldType.Description);
            Schema.AddField("Description", SemanticFieldType.Description);
            //Schema.AddField("URL", SemanticFieldType.Link);
            Schema.AddField("URL2", SemanticFieldType.Link);
            Schema.AddField("Tags", SemanticFieldType.Tags);
        }
    }
}
