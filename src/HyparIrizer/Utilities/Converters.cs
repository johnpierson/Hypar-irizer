using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Elements;
using Elements.Geometry;
using Curve = Autodesk.Revit.DB.Curve;
using Element = Autodesk.Revit.DB.Element;
using Line = Elements.Geometry.Line;
using Profile = Elements.Geometry.Profile;
using Units = Elements.Units;
using Wall = Autodesk.Revit.DB.Wall;

namespace HyparIrizer.Utilities
{
    static class Converters
    {
        public static WallByProfile RevitWallToHyparWall(Wall revitWall)
        {
            Document doc = revitWall.Document;

            //our outer polygon and the voids
            Polygon outerPolygon = null;
            List<Polygon> voids = new List<Polygon>();

            //use host object utils to get the outside face
            var exteriorFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Exterior);
            var interiorFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Interior);

            Element faceElement = doc.GetElement(exteriorFaces[0]);

            if (!(faceElement.GetGeometryObjectFromReference(exteriorFaces[0]) is Face exteriorFace) || !(faceElement.GetGeometryObjectFromReference(interiorFaces[0]) is Face interiorFace))
            {
                return null;
            }
            //this lets us pick the biggest face of the two sides. This is important because we want the walls to close. 😁
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
                    outerPolygon = new Polygon(vertices);
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
                    voids.Add(innerPolygon);
                }
            }
            //build our profile
            Profile prof = new Profile(outerPolygon,voids,Guid.NewGuid(), "revit Wall" );
            //get the location curve as an Elements.Line
            Curve curve = (revitWall.Location as LocationCurve).Curve;
            Line line = new Line(curve.GetEndPoint(0).ToVector3(true), curve.GetEndPoint(1).ToVector3(true));

            //return a neat Hypar wall
            return new WallByProfile(prof,revitWall.Width,line);
        }
        
        internal static Vector3 ToVector3(this XYZ xyz, bool scaleToMeters = false)
        {
            double num = (scaleToMeters ? Units.FeetToMeters(1) : 1);
            Vector3 vector3 = new Vector3(xyz.X, xyz.Y, xyz.Z) * num;
            return vector3;
        }
    }
}
