using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace DatastoreMiner
{
    /// <summary>
    /// Interface for catalogue readers which read a catalogue or manifest file of a Datastore and return the entries in a DataTable.
    /// The pattern for this is that a catalogue reader transforms the catalogue into a form useable by the DataStore processing classes.
    /// Catalogue reader could read from a CSV file, SPARQL endpoint or RDBMS.
    /// </summary>
    interface ICatalogueReader
    {
        DataTable ReadCatalogue(string CatalogueFile);
    }
}
