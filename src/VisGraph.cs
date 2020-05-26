using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickGraph;
using NetTopologySuite.Geometries;
using QuickGraph.Algorithms;
using System.Net.Http.Headers;
using OxyPlot.Series;
using OxyPlot;

namespace VisGraphs
{
    public class VisEdge : UndirectedEdge<Coordinate>
    {
        public double Length { get { return Source.Distance(Target); } }
        public VisEdge (Coordinate source, Coordinate target) : base(source, target)
        {

        }
    }
    public class VisGraph : UndirectedGraph<Coordinate, VisEdge>
    {
        public Polygon Polygon { get; }

        public IEnumerable<VisEdge> PolygonEdges
        {
            get
            {
                return Edges.Where(e => Polygon.Coordinates.Contains(e.Source) && Polygon.Coordinates.Contains(e.Target));
            }
        }
        public IEnumerable<VisEdge> ShellEdges
        {
            get
            {
                IEnumerable<Coordinate> shellCoords = Polygon.Shell.Coordinates;
                return Edges.Where(e => shellCoords.Contains(e.Source) && shellCoords.Contains(e.Target));
            }
        }
        public IEnumerable<VisEdge> HolesEdges
        {
            get
            {
                IEnumerable<Coordinate> holeCoords = Polygon.Holes.SelectMany(e => e.Coordinates);
                return Edges.Where(e => holeCoords.Contains(e.Source) && holeCoords.Contains(e.Target));
            }
        }
        public IEnumerable<Coordinate> ForeignVertices
        {
            get 
            {
                return Vertices.Where(e => !Polygon.Coordinates.Contains(e));
            }
        }
        public IEnumerable<VisEdge> ForeignEdges
        {
            get
            {
                return Edges.Where(e => ForeignVertices.Contains(e.Source) || ForeignVertices.Contains(e.Target));
            }
        }


        public VisGraph(Polygon polygon)
        {
            Polygon = polygon;
            BuildVisGraph();
        }
        private void BuildVisGraph()
        {
            if (Polygon.Shell != null)
            {
                AddVisEdgesByLinearRing(Polygon.Shell);
            }
            if (Polygon.Holes != null)
            {
                foreach (var ring in Polygon.Holes) AddVisEdgesByLinearRing(ring);
            }

            IEnumerable<Coordinate> coords = Polygon.Coordinates;
            for(int i=0; i< coords.Count(); i++)
            {
                Coordinate pt0 = coords.ElementAt(i);
                IEnumerable<VisEdge> edges = AdjacentEdges(pt0);
                VisEdge edgeF = edges.First();
                VisEdge edgeL = edges.Last();

                for (int j = i + 1; j < coords.Count(); j++)
                {
                    Coordinate pt1 = coords.ElementAt(j);

                    //If pt1 adjacent to pt0, jump to next pt1
                    if (edgeF.Source == pt1 || edgeF.Target == pt1 || edgeL.Source == pt1 || edgeL.Target == pt1) continue;

                    //pt1 not adjacent, test the line's relationship with polygon
                    if (IsVisible(pt0, pt1)) AddVerticesAndEdge(new VisEdge(pt0, pt1));
                }
            }
        }
        private void AddVisEdgesByLinearRing(LinearRing ring)
        {
            IEnumerable<Coordinate> coords = ring.Coordinates;
            for (int i = 0; i < coords.Count(); i++)
            {
                Coordinate source = coords.ElementAt(i);
                Coordinate target = coords.ElementAt((i + 1) % coords.Count());
                AddVerticesAndEdge(new VisEdge(source, target));
            }
        }

        public bool IsVisible(Coordinate pt0, Coordinate pt1)
        {
            Coordinate[] pts = new Coordinate[] { pt0, pt1 };
            LineString lineString = new LineString(pts);

            if (Polygon.Covers(lineString)) return true;

            return false;
        }
        public List<Coordinate> VisiblePointsByPoint(Coordinate pt0)
        {
            List<Coordinate> pts = new List<Coordinate>();
            for(int i=0; i < Vertices.Count(); i++)
            {
                Coordinate pt1 = Vertices.ElementAt(i);
                if (pt0 == pt1) continue;
                if (IsVisible(pt0, pt1)) pts.Add(pt1);
            }
            return pts;
        }

        public void AddVisEdgesForPoint(Coordinate pt0)
        {
            if (Polygon.Coordinates.Contains(pt0)) return;

            var visPoints = VisiblePointsByPoint(pt0);
            foreach (var pt1 in visPoints) AddVerticesAndEdge(new VisEdge(pt0, pt1));
        }
        public void AddVisEdgesForPoint(IEnumerable<Coordinate> pt0s)
        {
            foreach (var pt0 in pt0s)
            {
                if (Polygon.Coordinates.Contains(pt0)) continue;

                var visPoints = VisiblePointsByPoint(pt0);
                foreach (var pt1 in visPoints) AddVerticesAndEdge(new VisEdge(pt0, pt1));
            }
        }

        public IEnumerable<VisEdge> ShortestPathsDijkstraP2P(Coordinate pt0, Coordinate pt1)
        {
            AddVisEdgesForPoint(pt0);
            AddVisEdgesForPoint(pt1);
            try
            {
                Func<VisEdge, double> edgeCost = new Func<VisEdge, double>(e => e.Length);
                var tryFunc = this.ShortestPathsDijkstra(edgeCost, pt0);
                IEnumerable<VisEdge> res;
                tryFunc(pt1, out res);
                return res;
            }
            catch
            {
                return new List<VisEdge>();
            }
        }
        public List<IEnumerable<VisEdge>> ShortestPathsDijkstraP2P(Coordinate pt0, IEnumerable<Coordinate> pt1s)
        {
            AddVisEdgesForPoint(pt0);
            AddVisEdgesForPoint(pt1s);

            List<IEnumerable<VisEdge>> resAll = new List<IEnumerable<VisEdge>>();

            try
            {
                Func<VisEdge, double> func = new Func<VisEdge, double>(e => e.Length);
                var tryFunc = this.ShortestPathsDijkstra(func, pt0);
                foreach (Coordinate pt1 in pt1s)
                {
                    IEnumerable<VisEdge> res;
                    tryFunc(pt1, out res);
                    resAll.Add(res);
                }
            }
            catch
            {

            }

            return resAll;
        }


        public static List<LineSeries> LineSeriesByVisEdges(IEnumerable<VisEdge> edges)
        {
            List<LineSeries> allSeries = new List<LineSeries>();
            foreach (var edge in edges)
            {
                LineSeries series = LineSeriesByCoordinates(new Coordinate[] { edge.Source, edge.Target });
                allSeries.Add(series);
            }
            return allSeries;
        }
        public static LineSeries LineSeriesByCoordinates(IEnumerable<Coordinate> coords)
        {
            IEnumerable<DataPoint> pts = coords.Select(e => new DataPoint(e.X, e.Y));
            LineSeries lineSeries = new LineSeries();
            lineSeries.ItemsSource = pts;
            return lineSeries;
        }
    }
}
