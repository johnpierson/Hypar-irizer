using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.IFC;
using Elements;
using Elements.Geometry;
using Curve = Autodesk.Revit.DB.Curve;
using Element = Autodesk.Revit.DB.Element;
using Floor = Elements.Floor;
using Line = Elements.Geometry.Line;
using Material = Elements.Material;
using Panel = Autodesk.Revit.DB.Panel;
using Profile = Elements.Geometry.Profile;
using Units = Elements.Units;
using Wall = Autodesk.Revit.DB.Wall;

namespace HyparIrizer.Utilities
{
    static class Converters
    {
        public static Elements.Element RevitWallToHyparWall(Wall revitWall)
        {
            Polygon outerPolygon = null;
            List<Polygon> voids = new List<Polygon>();

            var polygons = revitWall.GetProfile();

            if (polygons == null)
            {
                return null;
            }

            outerPolygon = polygons[0];
            if (polygons.Count > 1)
            {
                voids.AddRange(polygons.Skip(1));
            }

            //build our profile
            Profile prof = new Profile(outerPolygon, voids, Guid.NewGuid(), "revit Wall");
            //get the location curve as an Elements.Line
            Curve curve = (revitWall.Location as LocationCurve).Curve;
            Line line = new Line(curve.GetEndPoint(0).ToVector3(true), curve.GetEndPoint(1).ToVector3(true));

            //return a neat Hypar wall
            return new WallByProfile(prof, revitWall.Width, line);

        }
        public static Elements.Element RevitFloorToHyparFloor(Autodesk.Revit.DB.Floor revitFloor)
        {
            Polygon outerPolygon = null;
            List<Polygon> voids = new List<Polygon>();
            var polygons = revitFloor.GetProfile();
            outerPolygon = polygons[0];
            if (polygons.Count > 1)
            {
                voids.AddRange(polygons.Skip(1));
            }

            //build our profile
            Profile prof = new Profile(outerPolygon, voids, Guid.NewGuid(), "revit Wall");
            Floor floor = new Floor(prof, revitFloor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble());

            //return a neat Hypar floor
            return floor;

        }

        public static Elements.ModelCurve CurtainGridToHyparCurve(Autodesk.Revit.DB.CurtainGridLine gridLine)
        {
            Elements.Geometry.Curve curve = new Line(gridLine.FullCurve.GetEndPoint(0).ToVector3(), gridLine.FullCurve.GetEndPoint(1).ToVector3());
            return new Elements.ModelCurve(curve,BuiltInMaterials.Edges);
        }

        public static Elements.Panel CurtainWallPanelToHyparPanel(Autodesk.Revit.DB.Panel panel)
        {
            //TODO:wrap this in a try catch for now until i build some actual handlers 
            try
            {
                Polygon poly = null;
                ElementId uGridId = ElementId.InvalidElementId;
                ElementId vGridId = ElementId.InvalidElementId;

                panel.GetRefGridLines(ref uGridId, ref vGridId);
                CurtainGrid hostingGrid = null;
                if (panel.Host is Wall wall)
                {
                    hostingGrid = wall.CurtainGrid;
                }

                CurtainCell cell = hostingGrid.GetCell(uGridId, vGridId);
                var loops = cell.CurveLoops;

                foreach (CurveArray loop in loops)
                {
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (Curve l in loop)
                    {
                        vertices.Add(l.GetEndPoint(0).ToVector3());
                    }
                    poly = new Polygon(vertices);
                }
                Elements.Panel newPanel = new Elements.Panel(poly,BuiltInMaterials.Glass);
                return newPanel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Space RevitRoomToHyparSpace(Room revitRoom)
        {
            var boundaries = revitRoom.GetBoundarySegments(new SpatialElementBoundaryOptions());

            Polygon outerLoop = null;
            List<Polygon> voids = new List<Polygon>();

            for (int i = 0; i < boundaries.Count; i++)
            {
                if (i == 0)
                {
                    var outer = boundaries[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (var segment in outer)
                    {
                        vertices.Add(segment.GetCurve().GetEndPoint(0).ToVector3());
                    }
                    outerLoop = new Polygon(vertices);
                }
                else
                {
                    var inner = boundaries[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (var segment in inner)
                    {
                        vertices.Add(segment.GetCurve().GetEndPoint(0).ToVector3());
                    }
                    Polygon innerPolygon = new Polygon(vertices);
                    voids.Add(innerPolygon);
                }
            }
            Profile profile = new Profile(outerLoop,voids,Guid.NewGuid(),"Revit Room");
            Space space = new Space(profile,revitRoom.UnboundedHeight);
            return space;
        }

        private static List<Polygon> GetProfile(this Autodesk.Revit.DB.Element element)
        {
            Document doc = element.Document;
            List<Polygon> polygons = new List<Polygon>();
            IList<Reference> firstSideFaces = null;
            IList<Reference> secondSideFaces = null;
            switch (element)
            {
                case Wall revitWall:
                    //use host object utils to get the outside face
                    firstSideFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Exterior);
                    secondSideFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Interior);
                    break;
                case Autodesk.Revit.DB.Floor revitFloor:
                    firstSideFaces = HostObjectUtils.GetTopFaces(revitFloor);
                    secondSideFaces = HostObjectUtils.GetBottomFaces(revitFloor);
                    break;
            }
            Element faceElement = doc.GetElement(firstSideFaces[0]);

            if (!(faceElement.GetGeometryObjectFromReference(firstSideFaces[0]) is Face exteriorFace) || !(faceElement.GetGeometryObjectFromReference(secondSideFaces[0]) is Face interiorFace))
            {
                return null;
            }
            //this lets us pick the biggest face of the two sides. This is important because we want the shapes to close. 😁
            Face face = exteriorFace.Area > interiorFace.Area ? exteriorFace : interiorFace;
            // get the edges as curve loops and use the IFCUtils to sort them
            // credit: https://thebuildingcoder.typepad.com/blog/2015/01/getting-the-wall-elevation-profile.html
            IList<CurveLoop> curveLoops = face.GetEdgesAsCurveLoops();
            //this does the sorting so outside is the first item
            IList<CurveLoop> loops = ExporterIFCUtils.SortCurveLoops(curveLoops)[0];

            for (int i = 0; i < loops.Count; i++)
            {
                //here for outermost loop
                if (i == 0)
                {
                    var outer = loops[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (Autodesk.Revit.DB.Curve c in outer)
                    {
                        vertices.Add(c.GetEndPoint(0).ToVector3());
                    }
                    polygons.Add(new Polygon(vertices));
                }
                //here for the inner loops (voids)
                else
                {
                    var inner = loops[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (Autodesk.Revit.DB.Curve c in inner)
                    {
                        vertices.Add(c.GetEndPoint(0).ToVector3());
                    }
                    Polygon innerPolygon = new Polygon(vertices);
                    polygons.Add(innerPolygon);
                }
            }

            return polygons;
        }

        internal static Vector3 ToVector3(this XYZ xyz, bool scaleToMeters = false)
        {
            double num = (scaleToMeters ? Units.FeetToMeters(1) : 1);
            Vector3 vector3 = new Vector3(xyz.X, xyz.Y, xyz.Z) * num;
            return vector3;
        }
    }
}
