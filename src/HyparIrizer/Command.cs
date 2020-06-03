using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Elements;
using Elements.Geometry.Profiles;
using HyparIrizer.Utilities;


namespace HyparIrizer
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            //access to the Revit selection methods
            Autodesk.Revit.UI.Selection.Selection sel = uidoc.Selection;

            //provides a filtered selection
            ElementMulticategoryFilter multicategoryFilter = new ElementMulticategoryFilter(TargetCategories());
            var filter =
                Utilities.SelFilter.GetElementFilter(multicategoryFilter);

            //prompt the user for a selection
            IList<Reference> selectionReference;
            try
            {
                selectionReference = sel.PickObjects(ObjectType.Element, filter);
            }
            catch (Exception)
            {
                //land here if someone cancels the pick operation
                return Result.Cancelled;
            }

            //try to export to JSON on the desktop
            bool result = RevitElementsToHypar(doc, selectionReference.Select(e => e.ElementId).ToList());

            if (result)
            {
                return Result.Succeeded;
            }
            return Result.Failed;
        }

        private bool RevitElementsToHypar( Document doc, List<ElementId> elementIds)
        {
            Model model = new Model();
            //iterate through and convert accordingly
            foreach (var id in elementIds)
            {
                var currentElement = doc.GetElement(id);

                switch (currentElement)
                {
                    case Autodesk.Revit.DB.Wall wall:
                       model.AddElement(Converters.RevitWallToHyparWall(wall));
                        break;
                    case Autodesk.Revit.DB.Floor floor:
                        model.AddElement(Converters.RevitFloorToHyparFloor(floor));
                        break;
                    case Autodesk.Revit.DB.Architecture.Room room:
                        model.AddElement(Converters.RevitRoomToHyparSpace(room));
                        break;
                    case Autodesk.Revit.DB.Panel panel:
                        model.AddElement(Converters.CurtainWallPanelToHyparPanel(panel));
                        break;
                    case Autodesk.Revit.DB.CurtainGridLine grid:
                        model.AddElement(Converters.CurtainGridToHyparCurve(grid));
                        break;
                }
            }
            
            //export the JSON to the desktop
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                File.WriteAllText(Path.Combine(path,$"{doc.Title}.json"),model.ToJson());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //our target categories for selection
        private List<ElementId> TargetCategories()
        {
            //filter for walls, and floors
            var categories = new List<ElementId> {new ElementId(-2000011), new ElementId(-2000032), new ElementId(-2000160), new ElementId(-2000170), new ElementId(-2000321) };

            return categories;
        }
    }
   
}
