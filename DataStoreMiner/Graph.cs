using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DatastoreMiner
{
    //Copy of Graph.cs from RSSKeywords 13 September 2011
    //Modified 17 September - Added Exists(VertexId) function
    //Modified 30 January 2012 - Added DeteteVertex
    //Modified 16 May 2012 - Added read/write labels, NCut code and write file for Pajek Net files and Gephi gefx files
    /// <summary>
    /// Graph structure and operations on the graph. Uses generic implementation so that user objects
    /// can be included on each node.
    /// 
    /// How it works:
    /// A Graph is a collection of Vertices (where the type T is the user data to be attached to each node)
    /// and Edges.
    /// A Vertex has a unique integer ID (not necessarily contiguous) and a list of In Edges and Out Edges.
    /// An Edge can be directed or undirected and has a string label, float weight and a vertex that it is
    /// connecting to and from. In the undirected case, from and to are interchangeable.
    /// 
    /// TODO: need to fix flatten as it outputs a single isolated node at the end of every polyline
    /// TODO: Fibonacci heap
    /// </summary>

    #region Edge Definition
    public class Edge<T> : IComparable<Edge<T>> where T: new()
    {
        private bool _IsDirected = false;
        private Vertex<T> _FromVertex;
        private Vertex<T> _ToVertex;
        private string _Label;
        private float _Weight;

        public bool IsDirected
        {
            get { return _IsDirected; }
        }

        public Vertex<T> FromVertex
        {
            get { return _FromVertex; }
        }

        public Vertex<T> ToVertex
        {
            get { return _ToVertex; }
        }

        public string Label
        {
            get { return _Label; }
            set { _Label = value; }
        }

        public float Weight
        {
            get { return _Weight; }
            set { _Weight = value; }
        }

        /// <summary>
        /// Edge constructor
        /// </summary>
        /// <param name="IsDirected">Whether this edge is directed or undirected</param>
        /// <param name="FromVertex">The vertex that this edge is connecting from</param>
        /// <param name="ToVertex">The vertex that this edge is connecting to</param>
        /// <param name="Label">A label for the edge</param>
        /// <param name="Weight">A weight for the edge</param>
        public Edge(bool IsDirected, Vertex<T> FromVertex, Vertex<T> ToVertex, string Label, float Weight)
        {
            _IsDirected = IsDirected;
            _FromVertex = FromVertex;
            _ToVertex = ToVertex;
            _Label = Label;
            _Weight = Weight;
        }

        /// <summary>
        /// Compare operator - edges are sorted by weight
        /// </summary>
        /// <param name="Other">The other edge to compare this weight to</param>
        /// <returns>Which edge is the lowest cost - this is a backwards comparison, but it makes sense for edge weights as less is more important</returns>
        public int CompareTo(Edge<T> Other)
        {
            return Weight.CompareTo(Other.Weight);
        }
    }
    #endregion

    #region Vertex Definition
    public class Vertex<T> where T: new()
    {
        private List<Edge<T>> _OutEdges=new List<Edge<T>>(); //are we calling them edges, arcs or links?
        private List<Edge<T>> _InEdges = new List<Edge<T>>();
        private int _VertexId; //unique vertex id number e.g. 0,1,2,3...
        private string _VertexLabel; //string label for this vertex
        private T _UserData; //user data tagged to this node
        public Vertex<T> Root; //Root of vertex used for Kruskal
        public float Rank; //Rank of vertex used for Kruskal
        public float Distance; //Distance used in Dijkstra
        public Vertex<T> Previous; //Previous vertex used in Dijkstra path

        /// <summary>
        /// Get edges coming out of this vertex
        /// </summary>
        public List<Edge<T>> OutEdges
        {
            get { return _OutEdges; }
        }

        /// <summary>
        /// Get edges going into this vertex
        /// </summary>
        public List<Edge<T>> InEdges
        {
            get { return _InEdges; }
        }

        public int VertexId
        {
            get { return _VertexId; }
        }

        public string VertexLabel
        {
            get { return _VertexLabel; }
            set { _VertexLabel = value; }
        }

        public T UserData
        {
            get { return _UserData; }
            set { _UserData = value; }
        }
        
        public Vertex() {}
        public Vertex(int Id, T UserData)
        {
            this._VertexId = Id;
            this._UserData = UserData;
            this.Rank = 0;
            this.Root = this;
        }

        /// <summary>
        /// Recursive function for getting root of vertex. Used for Kruskal.
        /// </summary>
        /// <returns>The root node of the group of trees that this vertex belongs to</returns>
        public Vertex<T> GetRoot()
        {
            if (this.Root != this)
            {
                this.Root = this.Root.GetRoot();
            }
            return this.Root;
        }
    }
    #endregion

    #region Graph Definition
    public class Graph<T> where T: new()
    {
        private bool _IsDirected = false; //this is set in the constructor
        private int _VertexIdCounter = 0;
        private Dictionary<int,Vertex<T>> _Vertices; //the key is the VertexId
        private List<Edge<T>> _Edges; //master list of all graph edges

        /// <summary>
        /// Returns whether the graph is directed or not
        /// </summary>
        public bool IsDirected
        {
            get { return _IsDirected; }
        }

        /// <summary>
        /// Default index property to return a vertex by its unique integer id
        /// </summary>
        /// <param name="id">The integer id of the vertex</param>
        /// <returns>The node</returns>
        public Vertex<T> this[int id]
        {
            get { return _Vertices[id]; }
        }

        /// <summary>
        /// BE VERY CAREFUL USING THIS!
        /// It's tempting to think that the index is the unique vertex ID, but it's NOT. This returns a list of all vertices in the graph, so the index isn't necessarily
        /// the same as the vertex ID.
        /// Use G[VertexId] if that's what you want. Otherwise, for a simple list of vertices use this.
        /// Return a simple list of vertices that doesn't require you to know the unique id numbers
        /// </summary>
        public List<Vertex<T>> Vertices
        {
            get { return _Vertices.Values.ToList(); }
        }

        /// <summary>
        /// Return true if the given vertex id exists
        /// </summary>
        /// <param name="id">The integer id of the vertex to check the existence of</param>
        /// <returns>True if vertex id exists</returns>
        public bool Exists(int id)
        {
            return _Vertices.ContainsKey(id);
        }

        /// <summary>
        /// Get the number of vertices in this graph
        /// </summary>
        public int NumVertices
        {
            get { return _Vertices.Count; }
        }

        /// <summary>
        /// Enumerator for Vertices
        /// </summary>
        /// <returns></returns>
        public System.Collections.IEnumerator GetEnumerator()
        {
            //TODO: surely just return _Vertices.Values.Enumerator?
            foreach (Vertex<T> V in _Vertices.Values)
            {
                yield return V;
            }
        }

        /// <summary>
        /// Get the number of edges in this graph
        /// </summary>
        public int NumEdges
        {
            get { return _Edges.Count; }
        }

        /// <summary>
        /// Sum of weights of all edges connecting A to B
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public float w(Vertex<T> A, Vertex<T> B)
        {
            float Sum = 0;
            foreach (Edge<T> E in A.OutEdges)
            {
                if (E.ToVertex == B) Sum += E.Weight;
            }
            if (!_IsDirected)
            {
                foreach (Edge<T> E in A.InEdges)
                {
                    if (E.FromVertex == B) Sum += E.Weight;
                }
            }
            return Sum;
        }

        /// <summary>
        /// Normalised Cut, defined as:
        /// NCut(A,B) = w(A,B)/w(A,V) + w(A,B)/w(B,V)
        /// Where w(A,B) is the sum of the weights of all edges connecting A to B
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public float NCut(Vertex<T> A, Vertex<T> B)
        {
            float wAB = w(A, B);
            
            //all weights used by A
            float wAV = 0;
            foreach (Edge<T> E in A.OutEdges) wAV += E.Weight;
            if (_IsDirected) foreach (Edge<T> E in A.InEdges) wAV += E.Weight;

            //all weights used by B
            float wBV = 0;
            foreach (Edge<T> E in B.OutEdges) wBV += E.Weight;
            if (_IsDirected) foreach (Edge<T> E in B.InEdges) wBV += E.Weight;

            return (wAB / wAV) + (wAB / wBV);
        }

        /// <summary>
        /// Normalised association, defined as:
        /// NAssoc(A,B) = w(A,A)/w(A,V) + w(B,B)/w(B,V)
        /// Where (A,B) is the sum of the weights of all edges connecting A to B
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public float NAssoc(Vertex<T> A, Vertex<T> B)
        {
            float wAA = w(A, A);
            float wBB = w(B, B);

            //all weights used by A
            float wAV = 0;
            foreach (Edge<T> E in A.OutEdges) wAV += E.Weight;
            if (_IsDirected) foreach (Edge<T> E in A.InEdges) wAV += E.Weight;

            //all weights used by B
            float wBV = 0;
            foreach (Edge<T> E in B.OutEdges) wBV += E.Weight;
            if (_IsDirected) foreach (Edge<T> E in B.InEdges) wBV += E.Weight;

            return (wAA / wAV) + (wBB / wBV);
        }

        /// <summary>
        /// Initialise an empty graph
        /// </summary>
        public Graph(bool IsDirected)
        {
            _IsDirected = IsDirected;
            _Vertices = new Dictionary<int,Vertex<T>>();
            _Edges = new List<Edge<T>>();
        }

        /// <summary>
        /// Initialise a graph from a list of edges.
        /// </summary>
        /// <param name="EdgeMap">A list of node to node edge connections with the nodes identified by
        /// a unique integer code number. This node code number is preserved as the NodeId in the resulting
        /// graph.</param>
        public Graph(bool IsDirected, Dictionary<int, int> EdgeMap)
        {
            _IsDirected = IsDirected;

            foreach (KeyValuePair<int, int> KVP in EdgeMap)
            {
                int A = KVP.Key, B = KVP.Value;
                Vertex<T> VertexA,VertexB;
                if (_Vertices.ContainsKey(A)) VertexA = _Vertices[A];
                else
                {
                    VertexA = AddVertex(A, new T());
                }
                if (_Vertices.ContainsKey(B)) VertexB = _Vertices[B];
                else
                {
                    VertexB = AddVertex(B, new T());
                }
                ConnectVertices(A, B,"",0);
            }
        }

        /// <summary>
        /// Add a new vertex to the graph and return it. This vertex has a unique id number, but no edges yet,
        /// so these will have to be added.
        /// </summary>
        /// <param name="UserData">User defined data to be tagged onto the vertex</param>
        /// <returns>The new vertex</returns>
        public Vertex<T> AddVertex(T UserData)
        {
            return AddVertex(_VertexIdCounter++, UserData);
        }

        /// <summary>
        /// Overloaded add to allow users to specify their own unique id number
        /// TODO: need exception for if unique id constraint is violated
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="UserData"></param>
        /// <returns>The new vertex</returns>
        public Vertex<T> AddVertex(int Id, T UserData)
        {
            //if (_Vertices.ContainsKey(Id)) throw duplicate id exception
            Vertex<T> V = new Vertex<T>(Id, UserData);
            _Vertices.Add(V.VertexId, V);
            return V;
        }

        /// <summary>
        /// Delete a vertex and any edges connected to it using the integer Vertex Id.
        /// Assumes that "VertexId" actually exists.
        /// </summary>
        /// <param name="VertexId">The unique id of the vertex to delete</param>
        public void DeleteVertex(int VertexId)
        {
            Vertex<T> V = _Vertices[VertexId];
            //delete all edges coming into this vertex
            foreach (Edge<T> E in V.InEdges)
            {
                //make a new list of out edges for this vertex, but without the vertex we are in the process of removing
                List<Edge<T>> NewOutEdges = new List<Edge<T>>();
                foreach (Edge<T> OutE in E.FromVertex.OutEdges)
                {
                    if (OutE.ToVertex.VertexId != VertexId) NewOutEdges.Add(OutE);
                }
                E.FromVertex.OutEdges.Clear();
                E.FromVertex.OutEdges.InsertRange(0, NewOutEdges);
            }

            //delete all edges going out of this vertex
            foreach (Edge<T> E in V.OutEdges)
            {
                //make a new list of in edges for this vertex, but without the vertex we are in the process of removing
                List<Edge<T>> NewInEdges = new List<Edge<T>>();
                foreach (Edge<T> InE in E.FromVertex.InEdges)
                {
                    if (InE.FromVertex.VertexId != VertexId) NewInEdges.Add(InE);
                }
                E.FromVertex.InEdges.Clear();
                E.FromVertex.InEdges.InsertRange(0, NewInEdges);
            }

            //delete copies from the main edge list either going in or out of this vertex
            List<Edge<T>> NewEdges = new List<Edge<T>>();
            foreach (Edge<T> E in _Edges)
            {
                if ((E.FromVertex.VertexId != VertexId) && (E.ToVertex.VertexId != VertexId))
                    NewEdges.Add(E);
            }
            _Edges = NewEdges;

            //delete the vertex itself
            _Vertices.Remove(VertexId);
        }

        /// <summary>
        /// Overload to delete a vertex based on the user data attached to it.
        /// TODO: could return true to indicate success?
        /// </summary>
        /// <param name="UserData">The user data attached to the vertex to delete</param>
        public void DeleteVertex(T UserData)
        {
            foreach (Vertex<T> V in _Vertices.Values)
            {
                if (V.UserData.Equals(UserData))
                {
                    DeleteVertex(V.VertexId);
                    break;
                }
            }
        }

        /// <summary>
        /// Connect two vertices together with an edge. Handles directed or undirected correctly.
        /// </summary>
        /// <param name="VertexId1"></param>
        /// <param name="VertexId2"></param>
        /// <returns></returns>
        public Edge<T> ConnectVertices(int VertexId1, int VertexId2, string Label, float Weight)
        {
            Vertex<T> VertexA = _Vertices[VertexId1];
            Vertex<T> VertexB = _Vertices[VertexId2];
            //TODO: probably need to check that A and B exist?
            //I'm creating a directed edge here, so it's up to the user if he wants to create the
            //opposite link as well
            return ConnectVertices(VertexA, VertexB, Label, Weight);
        }

        /// <summary>
        /// Overload to add a label and weight when connecting nodes
        /// </summary>
        /// <param name="VertexA">From Vertex</param>
        /// <param name="VertexB">To Vertex</param>
        /// <param name="Label">Label for the edge</param>
        /// <param name="weight">Weight for the edge</param>
        /// <returns></returns>
        public Edge<T> ConnectVertices(Vertex<T> VertexA, Vertex<T> VertexB, string Label, float Weight)
        {
            //Create a directed or undirected edge based on the graph type
            Edge<T> E = new Edge<T>(_IsDirected, VertexA, VertexB, Label, Weight);
            _Edges.Add(E); //make sure you add it to the graph's master edge list
            //now add the in and out links to the two nodes so we can traverse it
            VertexA.OutEdges.Add(E);
            VertexB.InEdges.Add(E);
            //in the undirected case, the edge is marked as undirected and added as both an in and out
            //edge to both the A and B vertices. In graph traversal, this case must be checked for, as
            //the FromVertex and ToVertex can be traversed in either direction.
            if (!_IsDirected)
            {
                VertexA.InEdges.Add(E);
                VertexB.OutEdges.Add(E);
            }
            return E;
        }

        /// <summary>
        /// Delete an edge
        /// </summary>
        /// <param name="E"></param>
        public void DeleteEdge(Edge<T> E)
        {
            //NOTE: in the undirected case, the vertex is added to the in and out edges of BOTH vertices, so we have to delete them all
            Vertex<T> FromV = E.FromVertex;
            Vertex<T> ToV = E.ToVertex;
            FromV.OutEdges.Remove(E);
            ToV.InEdges.Remove(E);
            if (!E.IsDirected)
            {
                FromV.InEdges.Remove(E); //remove copy of un-directed edge coming in
                ToV.OutEdges.Remove(E); //remove copy of un-directed edge going out
            }
            _Edges.Remove(E);
        }

        /// <summary>
        /// Return a list of all K-Connected vertices i.e. all vertices connected to the parent vertex
        /// by &lt;=K edges
        /// </summary>
        /// <param name="K">Find all vertices from the parent vertex below or equal to this weight</param>
        /// <param name="ParentVertex">The vertex to start scanning from</param>
        /// <returns></returns>
        public Dictionary<Vertex<T>,float> KConnected(float K, Vertex<T> ParentVertex)
        {
            //See Kruskal's algorithm or Primm's algorithm for minimum spanning trees
            Dictionary<Vertex<T>,float> Path = new Dictionary<Vertex<T>,float>();
            Path.Add(ParentVertex,0);
            return Traverse(K,0,ParentVertex,Path);
        }

        /// <summary>
        /// Vertex traversal
        /// </summary>
        /// <param name="K">Find all paths less than or equal to this weight</param>
        /// <param name="k">Sum of weights up to the CurrentVertex</param>
        /// <param name="CurrentVertex">The vertes we're currently on</param>
        /// <param name="Path">The list of vertices and weights up to this point</param>
        /// <returns>A list of nodes visited from CurrentVertex less than or equal to weight K</returns>
        private Dictionary<Vertex<T>,float> Traverse(
            float K, float k,
            Vertex<T> CurrentVertex, Dictionary<Vertex<T>,float> Path)
        {
            //traverse all out edges from this vertex
            foreach (Edge<T> OutEdge in CurrentVertex.OutEdges)
            {
                float NewWeight = k + OutEdge.Weight;
                Vertex<T> NextVertex = OutEdge.ToVertex;
                if (!_IsDirected)
                {
                    //in the undirected case, an edge can point in either direction, so we need to look at
                    //both the FromVertex and ToVertex to determind direction - whichever isn't the current
                    //vertex must be the next one.
                    if (NextVertex == CurrentVertex) NextVertex = OutEdge.FromVertex; //go backwards
                }
                if (NewWeight <= K)
                {
                    if (Path.ContainsKey(OutEdge.ToVertex))
                    {
                        if ((NewWeight) < Path[OutEdge.ToVertex])
                        {
                            //found a lower weight way of getting to this vertex, so replace with lower weight
                            Path[OutEdge.ToVertex] = NewWeight;
                            Traverse(K, NewWeight, OutEdge.ToVertex, Path);
                        }
                    }
                    else
                    {
                        //new vertex found so add to list and continue traversal
                        Path.Add(OutEdge.ToVertex, NewWeight);
                        Traverse(K, NewWeight, OutEdge.ToVertex, Path);
                    }
                }
            }
            return Path;
        }

        /// <summary>
        /// Return minimum spanning tree using Kruskal's algorithm.
        /// See: http://www.codeproject.com/Articles/163618/Kruskal_Algorithm
        /// </summary>
        /// <returns>A minimum spanning tree</returns>
        /// TODO: this doesn't appear to work - vertices aren't being connected up correctly
        //public Graph<T> Kruskal()
        //{
        //    Graph<T> MST = new Graph<T>(false);
        //    foreach (Vertex<T> V in this) MST.AddVertex(V.VertexId, V.UserData); //add all existing vertices to our new MST Graph

        //    List<Edge<T>> SortedEdges = new List<Edge<T>>();
        //    foreach (Edge<T> E in _Edges) SortedEdges.Add(E); //this can't be the best way of doing a copy?
        //    SortedEdges.Sort();
        //    float TotalCost = 0;
        //    foreach (Edge<T> E in SortedEdges)
        //    {
        //        Vertex<T> RootA = E.FromVertex.GetRoot();
        //        Vertex<T> RootB = E.ToVertex.GetRoot();
        //        if (RootA.VertexId != RootB.VertexId)
        //        {
        //            TotalCost += E.Weight;
        //            MST.ConnectVertices(E.FromVertex, E.ToVertex, "", E.Weight);
        //            if (RootB.Rank < RootA.Rank)//is the rank of Root2 less than that of Root1 ?
        //            {
        //                RootB.Root = RootA;	//yes! then make Root1 the root of Root2 
        //                //(since it has the higher rank)
        //            }
        //            else //rank of Root2 is greater than or equal to that of Root1
        //            {
        //                RootA.Root = RootB;	//make Root2 the root of Root1
        //                if (RootA.Rank == RootB.Rank)//both ranks are equal ?
        //                {
        //                    RootA.Rank++;	//increment one of them, 
        //                    //we need to reach a single root for the whole tree
        //                }
        //            }
        //        }
        //    }
        //    return MST;
        //}

        /// <summary>
        /// Write out a Pajek NET network file for the contents of this graph.
        /// The filename format should be "*.net"
        /// Vertex Ids are re-mapped to the range [1..NumVertices] required for Pajek. Edges are written out with weights and labels.
        /// </summary>
        /// <param name="Filename"></param>
        public void WritePajekNETFile(string Filename)
        {   
            using (TextWriter writer = File.CreateText(Filename))
            {
                int NumVertices=this._Vertices.Count;
                
                //hold an array of integer vertex ids mapping the graph vertex id (which can be any int) onto [1..NumVertices]
                Dictionary<int,int> VertexID = new Dictionary<int,int>(); //key is the vertex id (any int), value is [1..NumVertices] for Pajek file
                
                writer.WriteLine("*Vertices "+NumVertices);
                int VertexNum=1;
                foreach (Vertex<T> V in _Vertices.Values)
                {
                    VertexID.Add(V.VertexId,VertexNum);
                    writer.WriteLine(VertexNum+" \""+V.VertexLabel+"\"");
                    ++VertexNum;
                }

                //arcs are the connections between two vertices which are not straight lines
                //and are directional
                writer.WriteLine("*Arcs");
                
                //edges are the connections between two vertices as straight lines
                //which appear to be non-directional
                writer.WriteLine("*Edges");
                foreach (Edge<T> E in _Edges)
                {
                    //vertex1 vertex2 weight l (letter L) "label"
                    writer.WriteLine(
                        string.Format("{0} {1} {2} l \"{3}\"",
                        VertexID[E.FromVertex.VertexId],VertexID[E.ToVertex.VertexId],E.Weight,E.Label) );
                }
                writer.Close();
              }
        }

        /// <summary>
        /// Write out Gephi network file format *.gexf
        /// </summary>
        /// <param name="Filename"></param>
        public void WriteGexfFile(string Filename)
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
            using (TextWriter writer = File.CreateText(Filename))
            {
                int NumVertices = this._Vertices.Count;

                //hold an array of integer vertex ids mapping the graph vertex id (which can be any int) onto [1..NumVertices]
                Dictionary<int, int> VertexID = new Dictionary<int, int>(); //key is the vertex id (any int), value is [1..NumVertices] for Pajek file

                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.WriteLine("<gexf xmlns=\"http://www.gexf.net/1.2draft\" version=\"1.2\">");
                writer.WriteLine("<meta lastmodifieddate=\"" + DateTime.Now.ToShortTimeString() +"\">");
                writer.WriteLine("<creator>CASA</creator>");
                writer.WriteLine("<description>Graph</description>");
                writer.WriteLine("</meta>");
                if (this._IsDirected)
                    writer.WriteLine("<graph mode=\"static\" defaultedgetype=\"directed\">");
                else
                    writer.WriteLine("<graph mode=\"static\" defaultedgetype=\"undirected\">");
                writer.WriteLine("<nodes>");
                
                int VertexNum = 0; //node ids starting at 0 for Gephi format
                foreach (Vertex<T> V in _Vertices.Values)
                {
                    VertexID.Add(V.VertexId, VertexNum);
                    writer.WriteLine(
                        string.Format("<node id=\"{0}\" label=\"{1}\" />",
                        VertexNum, V.VertexLabel) ); //TODO: label needs to be xml encoded
                    ++VertexNum;
                }
                writer.WriteLine("</nodes>");

                writer.WriteLine("<edges>");
                int EdgeId = 0;
                foreach (Edge<T> E in _Edges)
                {
                    writer.WriteLine(
                        string.Format("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\" weight=\"{3}\" label=\"{4}\" />",
                        EdgeId, VertexID[E.FromVertex.VertexId], VertexID[E.ToVertex.VertexId], E.Weight, E.Label));
                    ++EdgeId;
                }
                writer.WriteLine("</edges>");
                writer.WriteLine("</graph>");
                writer.WriteLine("</gexf>");
                writer.Close();
            }
        }

        /// <summary>
        /// Flatten the graph into a list of polylines.
        /// The resulting polylines are a list of type T nodes with a null delimiting separate polylines.
        /// </summary>
        /// <returns>A list of individual polylines as an ordered list of type T nodes. Breaks are marked with a null.</returns>
        public List<T> Flatten()
        {
            List<T> Polylines = new List<T>(); //the polyline list to be returned
            HashSet<int> Visited = new HashSet<int>(); //list of visited nodes

            foreach (Vertex<T> V in this.Vertices)
            {
                if (!Visited.Contains(V.VertexId))
                    ExPolyFollowLinks(V, ref Polylines, ref Visited);
            }
            return Polylines;
        }

        /// <summary>
        /// Recursive depth first polyline follower to turn a minimum spanning tree into a list of polylines.
        /// The lines are lists of Vertices with nulls delimiting the polyline breaks.
        /// </summary>
        /// <param name="V">The vertex to start from</param>
        /// <param name="Result">A list of Vertices forming polylines with a null delimiter between separate lines</param>
        private void ExPolyFollowLinks(Vertex<T> V, ref List<T> Result, ref HashSet<int> Visited)
        {
            if ((Visited.Contains(V.VertexId)) || (V.OutEdges.Count == 0))
            {
                Result.Add(V.UserData); //ends polyline at this node
                //Guard case: no more links to follow, so this is the end of the tree
                Result.Add(default(T)); //terminate polyline with a null
                return; //this is kind of superfluous
            }
            else
            {
                Visited.Add(V.VertexId);
                //follow each of the other child links in turn
                for (int i = 0; i < V.OutEdges.Count; i++)
                {
                    Result.Add(V.UserData);
                    ExPolyFollowLinks(V.OutEdges[i].ToVertex, ref Result, ref Visited);
                }
            }
        }

        /// <summary>
        /// Find shortest path between source and target vertices. Uses Dijkstra algorithm, but exits when shortest path between source and target is found rather than
        /// finding shortest paths between source and all other vertices.
        /// Edge weights are used as distances.
        /// TODO: this goes wrong if there is no path from source to target
        /// </summary>
        public List<Vertex<T>> ShortestPath_Dijkstra(Vertex<T> Source, Vertex<T> Target)
        {
            List<Vertex<T>> Result = new List<Vertex<T>>();

            //initialise
            HashSet<Vertex<T>> Q = new HashSet<Vertex<T>>(); //list sorted on distance would be better
            foreach (Vertex<T> V in this)
            {
                V.Distance = float.PositiveInfinity;
                V.Previous = null;
                Q.Add(V);
            }
            Source.Distance = 0; //distance from source to source

            //algorithm
            while (Q.Count > 0)
            {
                //U is the vertex in Q with the smallest distance
                Vertex<T> U = Q.ElementAt(0);
                for (int i = 1; i < Q.Count; i++)
                    if (Q.ElementAt(i).Distance < U.Distance) U = Q.ElementAt(i);
                if (float.IsPositiveInfinity(U.Distance)) break; //all remaining vertices are inaccessible from source
                Q.Remove(U);
                //Result.Add(U);
                if (U == Target)
                {
                    //we've hit the target vertex, so return the path
                    Vertex<T> V = U;
                    while (V != null)
                    {
                        Result.Insert(0,V); //add to head of the list as we're going backwards from target to source
                        V = V.Previous;
                    }
                    break;
                }
                foreach (Edge<T> E in U.OutEdges)
                {
                    //all edges from U to vertices still in the Q list
                    if (Q.Contains(E.ToVertex))
                    {
                        Vertex<T> V = E.ToVertex; //neighbour of U
                        float alt = U.Distance + E.Weight; //weights are used as distances
                        if (alt < V.Distance) //Relax u,v,a
                        {
                            V.Distance = alt;
                            V.Previous = U;
                            //reorder key V in Q - it's not a queue...
                        }
                    }
                }
                //in the case of a non-directed graph, go through all the in edges as well
                if (!this._IsDirected)
                {
                    foreach (Edge<T> E in U.InEdges)
                    {
                        //all edges from U to vertices still in the Q list
                        if (Q.Contains(E.FromVertex)) //NOTE FromVertex used here!
                        {
                            Vertex<T> V = E.FromVertex; //neighbour of U
                            float alt = U.Distance + E.Weight; //weights are used as distances
                            if (alt < V.Distance) //Relax u,v,a
                            {
                                V.Distance = alt;
                                V.Previous = U;
                                //reorder key V in Q - it's not a queue...
                            }
                        }
                    }
                }
            }
            return Result;
        }
    }
    #endregion
}
