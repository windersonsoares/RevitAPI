using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;

namespace DetalhamentoDeVergalhoes
{
    [TransactionAttribute(TransactionMode.Manual)]
    class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Pegar uidoc e doc
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {

                //Filtro para pegar apenas vergalhões
                ISelectionFilter selectionFilter = new vergalhaoSelectionFilter();

                //Pegar elemento
                Reference pickedObj = uidoc.Selection.PickObject(ObjectType.Element, selectionFilter, "Selecione uma vergalhão");
                ElementId eleId = pickedObj.ElementId;
                Element ele = doc.GetElement(eleId);
                Rebar rebar = ele as Rebar; //Pega o elemento selecionado como um vergalhão

                //Pegar o o ospedeiro do elemento
                ElementId hostId = rebar.GetHostId();
                Element hostEle = doc.GetElement(hostId);

                //Pegar a vista ativa e criar um plano com base em sua origem e direção
                View view = doc.ActiveView;
                ViewType viewType = view.ViewType;
                XYZ origem = view.Origin;
                XYZ direcao = view.ViewDirection;
                Plane plano = Plane.CreateByNormalAndOrigin(direcao, origem);

                //Forma do vergalhão
                ElementId shapeId = rebar.GetShapeId();
                ElementType shape = doc.GetElement(shapeId) as ElementType;
                
		//Cria o nome da família do item de detalhe, "Detalhe vergalhão 01", "Detalhe vergalhão 22", etc
		String shapeName = "Detalhe vergalhão " + shape.Name;

                //Dimensões do vergalhão, adicionar outras conforme for evoluindo, servirão para alterar a família de item de detalhe
                Double dA = ele.LookupParameter("A").AsDouble();
                Double dB = ele.LookupParameter("B").AsDouble();

                using (Transaction trans = new Transaction(doc, "Criar elemento"))
                {
                    trans.Start();
										
		    //Variável para guardar o sketchplane
                    SketchPlane sketchPlane;

                    //Pegar o SketchPlane de acordo com o tipo de vista, se for elevação ou corte o SketchPlane será a partir do plano criado anteriormente
	            if (viewType == ViewType.Elevation || viewType == ViewType.Section)
                    {
                        sketchPlane = SketchPlane.Create(doc, plano);
                    }
                    else
                    {
                        sketchPlane = view.SketchPlane;
                    }

                    //Define o SketchPlane da vista
		    view.SketchPlane = sketchPlane;

                    //Procura a família de item de detalhe com base no nome e ativa o mesmo
		    FilteredElementCollector collector = new FilteredElementCollector(doc);
                    IList<Element> symbols = collector.OfClass(typeof(FamilySymbol)).WhereElementIsElementType().ToElements();

                    FamilySymbol symbol = null;
                    foreach (Element elem in symbols)
                    {
                        if (elem.Name == shapeName)
                        {
                            symbol = elem as FamilySymbol;
                            break;
                        }
                    }

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                    }

                    //Pega o ponto selecionado
		    XYZ pickedPoint = uidoc.Selection.PickPoint();
										
		    //Cria o item de detalhe no ponto e define seus parâmetros
                    FamilyInstance familyInstance = doc.Create.NewFamilyInstance(pickedPoint,symbol,view);
                    familyInstance.LookupParameter("A").Set(dA);
                    familyInstance.LookupParameter("B").Set(dB);

                    trans.Commit();
                }
                
                return Result.Succeeded;
            }
            catch (Exception e)
            {

                message = e.Message;
                return Result.Failed;
            }
        }
    }

    //CLASSE QUE MODIFICA A SELEÇÃO PARA APENAS SELECIONAR VERGALHOES
    public class vergalhaoSelectionFilter : ISelectionFilter
    {
        //Determina se o elemento deve ser aceito pelo filtro
        public bool AllowElement(Element element)
        {
            //Convert o elemento para um vergalhão
            Rebar verg = element as Rebar;

            //verg será null se o elemento não for um vergalhão
            if (verg == null)
            {
                //retornar falso
                return false;
            }

            //se verg for um vergalhão retornar verdadeiro
            return true;
        }
        //Referências nunca serão aceitas por esse filtro
        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
}
