using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickGraph;
using NetTopologySuite.Geometries;
using OxyPlot;
using OxyPlot.Series;

namespace VisGraph
{
    public class VisEdge : UndirectedEdge<Coordinate>
    {
        public double Lenght { get { return Source.Distance(Target); } }
        public VisEdge (Coordinate source, Coordinate target) : base(source, target)
        {

        }
    }
    public class VisGraph : UndirectedGraph<Coordinate, VisEdge>
    {
        public Polygon Polygon { get; }
        public PlotModel PlotModel { get; }


        public VisGraph(Polygon polygon)
        {
            Polygon = polygon;
            if (Polygon.Shell != null)
            {
                AddVerticesAndEdgeByRing(Polygon.Shell);
            }
            if (Polygon.Holes != null)
            {
                foreach (var ring in Polygon.Holes) AddVerticesAndEdgeByRing(ring);
            }
            BuildVisGraph();

            PlotModel = new PlotModel();
            BuildPlotModel();
        }

        private void BuildVisGraph()
        {
            IEnumerable<Coordinate> coords = Polygon.Coordinates;
            for(int i=0; i< coords.Count(); i++)
            {
                Coordinate pt0 = coords.ElementAt(i);
                IEnumerable<VisEdge> edges = AdjacentEdges(pt0);
                VisEdge edgeF = edges.First();
                VisEdge edgeL = edges.Last();

                for (int j=i+1; j< coords.Count(); j++)
                {
                    Coordinate pt1 = coords.ElementAt(j);

                    //If pt1 adjacent to pt0, jump to next pt1
                    if (edgeF.Source == pt1 || edgeF.Target == pt1 || edgeL.Source == pt1 || edgeL.Target == pt1) continue;

                    //pt1 not adjacent, test the line's relationship with polygon
                    if (IsVisible(pt0, pt1, Polygon)) AddVerticesAndEdge(new VisEdge(pt0, pt1));
                }
            }
        }
        private void AddVerticesAndEdgeByRing(LinearRing ring)
        {
            IEnumerable<Coordinate> coords = ring.Coordinates;
            for (int i = 0; i < coords.Count(); i++)
            {
                Coordinate source = coords.ElementAt(i);
                Coordinate target = coords.ElementAt((i + 1) % coords.Count());
                AddVerticesAndEdge(new VisEdge(source, target));
            }
        }
        public bool IsVisible(Coordinate pt0, Coordinate pt1, Polygon polygon)
        {
            Coordinate[] pts = new Coordinate[] { pt0, pt1 };
            LineString lineString = new LineString(pts);

            if (polygon.Covers(lineString)) return true;

            return false;
        }

        private void BuildPlotModel()
        {
            foreach(VisEdge edge in Edges)
            {
                LineSeries series = LineSeriesByCoordinates(new Coordinate[] { edge.Source, edge.Target });
                series.Color = OxyColors.Green;
                PlotModel.Series.Add(series);
            }
            if (Polygon.Shell != null)
            {
                LineSeries series = LineSeriesByCoordinates(Polygon.Shell.Coordinates);
                series.Color = OxyColors.Blue;
                series.LineStyle = LineStyle.Solid;
                PlotModel.Series.Add(series);
            }
            if (Polygon.Holes != null)
            {
                foreach (LinearRing ring in Polygon.Holes)
                {
                    LineSeries series = LineSeriesByCoordinates(ring.Coordinates);
                    series.Color = OxyColors.Red;
                    series.LineStyle = LineStyle.Solid;
                    PlotModel.Series.Add(series);
                }
            }

        }
        private LineSeries LineSeriesByCoordinates(IEnumerable<Coordinate> coords)
        {
            IEnumerable<DataPoint> pts = coords.Select(e => new DataPoint(e.X, e.Y));
            LineSeries lineSeries = new LineSeries();
            lineSeries.ItemsSource = pts;
            return lineSeries;
        }
    }
}
