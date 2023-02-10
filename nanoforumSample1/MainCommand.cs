using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using nanoforumSample1.Entities;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;

namespace nanoforumSample1
{
    public class MainCommand
    {
        [CommandMethod("фв", CommandFlags.UsePickSet |
                         CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void addEntity()
        {
            // Сначала получаем БД текущего чертежа
            Database dbCurrent = Application.DocumentManager.MdiActiveDocument.Database;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Document doc = Application.DocumentManager.MdiActiveDocument;

            //Выбрать объект

            //Окно приглашения
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите объект: ");

            //Для функции результа 
            PromptEntityResult per = ed.GetEntity(peo);

            //Проверка что выбрано
            using (Transaction trAdding = dbCurrent.TransactionManager.StartTransaction())
            {

                CreatLayer("Узлы_Saidi_Saifi_AeroHost", 0, 127, 0, ed, dbCurrent, trAdding);
                CreatLayer("Граф_Saidi_Saifi_AeroHost", 76, 153, 133, ed, dbCurrent, trAdding);
                CreatLayer("НазванияЛиний_Saidi_Saifi_AeroHost", 0, 191, 255, ed, dbCurrent, trAdding);
                CreatLayer("Ребра_Saidi_Saifi_AeroHost", 255, 191, 0, ed, dbCurrent, trAdding);

                Entity btrCurrSpace = trAdding.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;

                //ID Выбранного объекта
                ed.WriteMessage("\n ID Выбранного объекта: " + per.ObjectId.ToString());

                //Создать магистраль
                PowerLine Magistral = new PowerLine { Name = "Магистраль", IDLine = per.ObjectId };

                //Ищем отпайки
                SearchPlyline(Magistral, ed, trAdding, Magistral);

                //Ищем ответвления от отпаек
                List<List<PowerLine>> LispAllLine = GenaratePowerLineClass(Magistral, ed, trAdding, Magistral);

                // Добавка в весь список магистраль
                LispAllLine[0].Insert(0, Magistral);

                //Создает наим линий
                CreatTextFromLine("НазванияЛиний_Saidi_Saifi_AeroHost", LispAllLine[0], ed, dbCurrent, trAdding); //Вставить надо лист классов

                //Создает наим узлов
                List<Point3d> listPointKnot = CreatTextFromKnot("Узлы_Saidi_Saifi_AeroHost", LispAllLine[0], dbCurrent, ed, trAdding); //Вставить надо лист классов

                //Выделение все полиллиний в LispAllLine[0]
                SelectObjectFromListClass(LispAllLine[0], ed, dbCurrent, trAdding);

                //Создать копию полиллинии, перенести на другой слой,
                CopySelect("Граф_Saidi_Saifi_AeroHost", ed, dbCurrent, trAdding);

                //Выделить все созданные полиллинии в слои	
                SelectObjectLayer("Граф_Saidi_Saifi_AeroHost", "LWPOLYLINE", ed);

                // Expload объектов
                ExploadSelectObject(dbCurrent, ed, trAdding);

                //Выделить все созданные линии в слои
                SelectObjectLayer("Граф_Saidi_Saifi_AeroHost", "LWPOLYLINE", ed);

                //Затереть
                ed.Command("ERASE");

                //Выделить все созданные линии в слои
                SelectObjectLayer("Граф_Saidi_Saifi_AeroHost", "LINE", ed);

                List<Edge> listLine = CreatClassEdgeList(ed);

                CreatTextFromEdge("Ребра_Saidi_Saifi_AeroHost", listLine, ed, dbCurrent, trAdding);




                trAdding.Commit();
            }






        }

        public void Initialize()
        {
        }

        void SearchPlyline(PowerLine considerPowerLine, Editor ed, Transaction trAdding, PowerLine lineMagistral)
        {

            Polyline Plyline = trAdding.GetObject(considerPowerLine.IDLine, OpenMode.ForRead) as Polyline;
            for (int i = 0; i < Plyline.NumberOfVertices; i++)
            {
                // Информация о поинте
                Point2d vertex = Plyline.GetPoint2dAt(i);

                //Добавить в листпоинтов
                considerPowerLine.Point.Add(new Point2d(vertex.X, vertex.Y));

                // Поиск других полилиний вблизи текущей вершины
                Point3d searchPoint = new Point3d(vertex.X, vertex.Y, 0);
                SelectionFilter acSF = new SelectionFilter(new TypedValue[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
                PromptSelectionResult acPSR = ed.SelectCrossingWindow(searchPoint, searchPoint + new Vector3d(1, 1, 0), acSF);

                // Проверьте, были ли найдены какие-либо другие полилинии
                if (acPSR.Status == PromptStatus.OK)
                {
                    // Пройдите по найденным объектам
                    foreach (SelectedObject acSObj in acPSR.Value)
                    {
                        //Отсечь магистраль
                        if (Plyline.ObjectId != acSObj.ObjectId)
                        {
                            if (acSObj.ObjectId != lineMagistral.IDLine)
                            {
                                considerPowerLine.TapsID.Add(acSObj.ObjectId);
                                // Получить найденный объект
                                Polyline foundPl = trAdding.GetObject(acSObj.ObjectId, OpenMode.ForRead) as Polyline;
                            }

                            // Выведите информацию о найденном объекте
                            // ed.WriteMessage("\n Другая ломаная линия, найденная в вершине " + (i + 1) + ": " + foundPl.ObjectId.ToString());
                        }
                    }
                }

            }


        }

        void InfoMessage(PowerLine considerPowerLine, Editor ed)
        {
            ed.WriteMessage("---------------------------------------------");
            ed.WriteMessage("|ID Линии:" + considerPowerLine.IDLine + "|");
            ed.WriteMessage("---------------------------------------------");
            ed.WriteMessage("Имя линии: " + considerPowerLine.Name);
            if (considerPowerLine.Parent != ObjectId.Null)
            {
                ed.WriteMessage("Родитель: " + considerPowerLine.Parent);

            }
            ed.WriteMessage("Количество вершин: " + considerPowerLine.Point.Count.ToString());

            if (considerPowerLine.TapsID.Count != 0)
            {
                ed.WriteMessage("Количество отпаек: " + considerPowerLine.TapsID.Count.ToString());

                string result = "";
                foreach (ObjectId ObjId in considerPowerLine.TapsID)
                {
                    result = result + ";" + ObjId.ToString();
                }

                ed.WriteMessage("ID отпаек" + result);
            }
        }

        public List<List<PowerLine>> GenaratePowerLineClass(PowerLine considerPowerLine, Editor ed, Transaction tran, PowerLine lineMagistral)
        {
            List<List<PowerLine>> lisiofListesLine = new List<List<PowerLine>>();
            List<PowerLine> listAllLines = new List<PowerLine>();
            List<PowerLine> listLines2 = new List<PowerLine>();
            List<PowerLine> listLines3 = new List<PowerLine>();
            List<PowerLine> listLines4 = new List<PowerLine>();
            List<PowerLine> listLines5 = new List<PowerLine>();
            //Поиск отпаек
            for (int i = 0; i < considerPowerLine.TapsID.Count; i++)
            {
                PowerLine TapsLines = new PowerLine();
                TapsLines.Name = "Отпайка № " + (i + 1); //тут добавил +1 дня номр нумерации
                TapsLines.IDLine = considerPowerLine.TapsID[i];
                TapsLines.Parent = considerPowerLine.IDLine;
                TapsLines.ParentName = considerPowerLine.ParentName;
                SearchPlyline(TapsLines, ed, tran, lineMagistral);
                listAllLines.Add(TapsLines);


                //Найти ответвления от отпаек
                if (TapsLines.TapsID.Count != 0)
                {
                    for (int j = 0; j < TapsLines.TapsID.Count; j++)
                    {
                        PowerLine TapsLines2 = new PowerLine();
                        TapsLines2.Name = "Ответвление № " + (j + 1) + " от Отпайки № " + (i + 1);

                        TapsLines2.IDLine = TapsLines.TapsID[j];
                        TapsLines2.Parent = TapsLines.IDLine;
                        TapsLines2.ParentName = TapsLines.ParentName;
                        SearchPlyline(TapsLines2, ed, tran, TapsLines);
                        listLines2.Add(TapsLines2);

                        // найти ответвления от ответвлений
                        if (TapsLines2.TapsID.Count != 0)
                        {

                            for (int k = 0; k < TapsLines2.TapsID.Count; k++)
                            {
                                PowerLine TapsLines3 = new PowerLine();
                                TapsLines3.Name = "Ответвление № " + (k + 1) + " от Ответвления №" + (j + 1) + " от Отпайки № " + (i + 1); //тут добавил +1 дня номр нумерации
                                TapsLines3.IDLine = TapsLines2.TapsID[k];
                                TapsLines3.Parent = TapsLines2.IDLine;
                                TapsLines3.ParentName = TapsLines2.ParentName;
                                SearchPlyline(TapsLines3, ed, tran, TapsLines2);
                                listLines3.Add(TapsLines3);

                                if (TapsLines3.TapsID.Count != 0)
                                {
                                    for (int l = 0; l < TapsLines3.TapsID.Count; l++)
                                    {
                                        /*ed.WriteMessage("\n TapsLines3.Name"+ TapsLines3.Name.ToString()
															+"\n l :"+l
											                +"\n TapsLines3.TapsID.Count: из "+TapsLines3.TapsID.Count);*/


                                        PowerLine TapsLines4 = new PowerLine();
                                        TapsLines4.Name = "Ответвление № " + (l + 1) + " от ответвления № " + (k + 1) + " от Ответвления №" + (j + 1) + " от Отпайки № " + (i + 1); //тут добавил +1 дня номр нумерации
                                        TapsLines4.IDLine = TapsLines3.TapsID[l];
                                        TapsLines4.Parent = TapsLines3.IDLine;
                                        TapsLines4.ParentName = TapsLines3.ParentName;
                                        SearchPlyline(TapsLines4, ed, tran, TapsLines3);
                                        listLines4.Add(TapsLines4);

                                        if (TapsLines4.TapsID.Count != 0)
                                        {
                                            for (int q = 0; q < TapsLines4.TapsID.Count; q++)
                                            {
                                                PowerLine TapsLines5 = new PowerLine();
                                                TapsLines5.Name = "Отв. № " + (q + 1) + " Отв. № " + (l + 1) + " от Отв. № " + (k + 1) + " от Отв. №" + (j + 1) + " от Отп. № " + (i + 1); //тут добавил +1 дня номр нумерации
                                                TapsLines5.IDLine = TapsLines4.TapsID[q];
                                                TapsLines5.Parent = TapsLines4.IDLine;
                                                TapsLines5.ParentName = TapsLines4.ParentName;
                                                SearchPlyline(TapsLines5, ed, tran, TapsLines4);
                                                listLines5.Add(TapsLines5);
                                            }
                                        }

                                    }
                                }
                            }


                        }
                    }

                }


            }


            listAllLines.AddRange(listLines2);
            listAllLines.AddRange(listLines3);
            listAllLines.AddRange(listLines4);
            listAllLines.AddRange(listLines5);

            lisiofListesLine.Add(listAllLines);
            lisiofListesLine.Add(listLines2);
            lisiofListesLine.Add(listLines3);
            lisiofListesLine.Add(listLines4);
            lisiofListesLine.Add(listLines5);
            return lisiofListesLine;
        }

        public void CreatLayer(string Name, byte ColorR, byte ColorG, byte ColorB, Editor ed, Database dbCurrent, Transaction trAdding)
        {

            LayerTable layerTable = trAdding.GetObject(dbCurrent.LayerTableId, OpenMode.ForWrite) as LayerTable;

            if (!layerTable.Has(Name))
            {
                // Создание слоя
                LayerTableRecord acLyrTblRec = new LayerTableRecord();
                acLyrTblRec.Name = Name;
                acLyrTblRec.Color = Teigha.Colors.Color.FromRgb(ColorR, ColorG, ColorB);
                layerTable.UpgradeOpen();
                ObjectId acObjId = layerTable.Add(acLyrTblRec);
                trAdding.AddNewlyCreatedDBObject(acLyrTblRec, true);
                ed.WriteMessage("\nСлой создан: " + Name + " !!!Не удаляйте данный слой!!");
            }
            else
            {
                // ed.WriteMessage("\nСлой уже существует: " + Name);
            }


        }

        public void SelectLayer(string Name, Editor ed, Database dbCurrent, Transaction trAdding)
        {

            LayerTable layerTable = trAdding.GetObject(dbCurrent.LayerTableId, OpenMode.ForWrite) as LayerTable;
            if (layerTable.Has(Name))
            {
                ObjectId acObjId = layerTable[Name];
                LayerTableRecord acLyrTblRec = trAdding.GetObject(acObjId, OpenMode.ForWrite) as LayerTableRecord;
                dbCurrent.Clayer = acObjId;

            }


        }

        public List<Point3d> CreatTextFromKnot(string nameSearchLayer, List<PowerLine> masterLine, Database dbCurrent, Editor ed, Transaction trAdding)
        {
            object[,] resultKnotPoint = new object[,] { };
            List<Point3d> resultKnotPoint2 = new List<Point3d>();


            // Ищу на какой слой закинуть
            LayerTable acLyrTbl = trAdding.GetObject(dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;

            ObjectId acLyrId = ObjectId.Null;
            if (acLyrTbl.Has(nameSearchLayer))
            {
                acLyrId = acLyrTbl[nameSearchLayer];
            }

            // что б стащить цвет
            LayerTableRecord acLyrTblRec = trAdding.GetObject(acLyrId, OpenMode.ForRead) as LayerTableRecord;

            // Для того что бы закинуть текст
            BlockTable acBlkTbl = trAdding.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;

            BlockTableRecord acBlkTblRec = trAdding.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (PowerLine element in masterLine)
            {
                for (int i = 0; i < element.Point.Count; i++)
                {
                    resultKnotPoint2.Add(new Point3d(element.Point[i].X, element.Point[i].Y, 0));
                }
                resultKnotPoint2 = resultKnotPoint2.Distinct().ToList();
            }

            for (int i = 0; i < resultKnotPoint2.Count; i++)
            {// Характеристики текста
                MText acMText = new MText();
                acMText.Color = acLyrTblRec.Color; //Цвето по слою
                acMText.Location = resultKnotPoint2[i];
                acMText.Contents = (i + 1).ToString();// +1 для визуализации
                acMText.TextHeight = 5; //Размер шрифта
                acMText.Height = 5; //Высота  ?
                acMText.LayerId = acLyrId;
                //acMText.Layer = "MyLayer"; // Можно и так на слой
                //acMText.Attachment = AttachmentPoint.MiddleCenter; //Центровка текста

                acBlkTblRec.AppendEntity(acMText);
                trAdding.AddNewlyCreatedDBObject(acMText, true);

            }

            return resultKnotPoint2;
        }



        public void CreatTextFromLine(string nameSearchLayer, List<PowerLine> listPowerLineCrearTest, Editor ed, Database dbCurrent, Transaction trAdding)
        {


            // Ищу на какой слой закинуть
            LayerTable acLyrTbl = trAdding.GetObject(dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;
            ObjectId acLyrId = ObjectId.Null;
            if (acLyrTbl.Has(nameSearchLayer))
            {
                acLyrId = acLyrTbl[nameSearchLayer];
            }

            // что б стащить цвет
            LayerTableRecord acLyrTblRec = trAdding.GetObject(acLyrId, OpenMode.ForRead) as LayerTableRecord;

            // Для того что бы закинуть текст
            BlockTable acBlkTbl = trAdding.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;

            BlockTableRecord acBlkTblRec = trAdding.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (PowerLine element in listPowerLineCrearTest)
            {
                // Характеристики текста
                MText acMText = new MText();
                acMText.Color = acLyrTblRec.Color; //Цвето по слою
                acMText.Location = new Point3d(element.Point[0].X, element.Point[0].Y, 0);
                acMText.Contents = element.Name.ToString();// +1 для визуализации
                acMText.TextHeight = 4; //Размер шрифта
                acMText.Height = 4; //Высота  ?
                acMText.LayerId = acLyrId;
                //acMText.Layer = "MyLayer"; // Можно и так на слой
                //acMText.Attachment = AttachmentPoint.MiddleCenter; //Центровка текста

                acBlkTblRec.AppendEntity(acMText);
                //trAdding.AddNewlyCreatedDBObject(acMText, true);

            }
        }


        public void SelectObjectFromListClass(List<PowerLine> selectPowerLine, Editor ed, Database dbCurrent, Transaction trAdding)
        {

            List<ObjectId> listObID = new List<ObjectId>();

            foreach (PowerLine element2 in selectPowerLine)
            {
                listObID.Add(element2.IDLine);
            }
            ed.SetImpliedSelection(listObID.ToArray());//Функция выделения


        }


        public class PowerLine
        {
            public string Name { get; set; }
            public List<Point2d> Point { get; set; }
            public ObjectId IDLine { get; set; }
            //public string SigmentLengt { get; set; }
            //public string LengtPowerLine { get; set; }
            public ObjectId Parent { get; set; }
            public string ParentName { get; set; }
            public List<ObjectId> TapsID { get; set; }
            public List<string> TapsName { get; set; }

            public PowerLine()
            {
                Name = null;
                Point = new List<Point2d>();
                IDLine = ObjectId.Null;
                Parent = ObjectId.Null;
                ParentName = null;
                TapsID = new List<ObjectId>();
                TapsName = new List<string>();
            }

        }


        public void SelectObjectFromListID(List<ObjectId> selectListID, Editor ed)
        {
            ed.SetImpliedSelection(selectListID.ToArray()); //Функция выделения
        }


        public void CopySelect(string NameLayer, Editor ed, Database dbCurrent, Transaction trAdding)
        {


            List<ObjectId> ListObjIDLine = new List<ObjectId>();
            PromptSelectionResult acSSPrompt = ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;
                BlockTable acBlkTbl = trAdding.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec = trAdding.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        Entity acEnt = trAdding.GetObject(acSSObj.ObjectId, OpenMode.ForRead) as Entity;
                        // Make a copy of the selected object

                        Entity acEntCopy = acEnt.Clone() as Entity;
                        // Change the layer of the copy                      	
                        acEntCopy.Layer = NameLayer;

                        ListObjIDLine.Add(acEntCopy.ObjectId);

                        // Add the new object to Model space
                        acBlkTblRec.AppendEntity(acEntCopy);
                    }
                }
            }


        }

        public void SelectObjectLayer(string nameLayer, string typeObject, Editor ed)
        {


            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(new TypedValue[]
            {
                    new TypedValue((int)DxfCode.LayerName, nameLayer),
                    new TypedValue((int)DxfCode.Start, typeObject),
                // new TypedValue((int)DxfCode.LayerName, nameLayer)
            }
            ));
            if (res.Status == PromptStatus.OK)
            {
                ed.SetImpliedSelection(res.Value.GetObjectIds());
            }
        }

        public void ExploadSelectObject(Database dbCurrent, Editor ed, Transaction trAdding)
        {

            PromptSelectionResult ss = ed.GetSelection();


            if (ss.Status == PromptStatus.OK)
            {
                DBObjectCollection objectsToExplodeList = new DBObjectCollection();

                SelectionSet acSSet3 = ss.Value;

                foreach (SelectedObject so in acSSet3)
                {
                    Entity ent = (Entity)trAdding.GetObject(so.ObjectId, OpenMode.ForWrite);
                    ent.Explode(objectsToExplodeList);

                }

                BlockTableRecord btr = (BlockTableRecord)trAdding.GetObject(dbCurrent.CurrentSpaceId, OpenMode.ForWrite);
                foreach (DBObject obj in objectsToExplodeList)

                {
                    Entity ent = (Entity)obj;
                    btr.AppendEntity(ent);
                    trAdding.AddNewlyCreatedDBObject(ent, false); //flase Не оставлять оригинал, не работает !?


                }

            }
        }


        public class Edge
        {
            public string Name { get; set; }
            public Point3d StartPoint { get; set; }
            public Point3d CentrPoint { get; set; }
            public Point3d EndPoint { get; set; }

            public ObjectId IDLine { get; set; }



            public Edge()
            {
                Name = null;
                IDLine = ObjectId.Null;
                StartPoint = new Point3d();
                EndPoint = new Point3d();
                CentrPoint = new Point3d();

            }

        }

        public List<Edge> CreatClassEdgeList(Editor ed)
        {
            List<Line> edgesList = new List<Line>();
            List<Edge> edgesList2 = new List<Edge>();
            PromptSelectionResult acSSPrompt = ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                foreach (SelectedObject acSObj in acSSPrompt.Value)
                {
                    Entity ent = (Entity)acSObj.ObjectId.GetObject(OpenMode.ForWrite) as Line;

                    edgesList.Add((Line)ent);

                }

                for (int i = 0; i < edgesList.Count(); i++)
                {
                    Edge line = new Edge();
                    line.Name = (i + 1).ToString();
                    line.IDLine = edgesList[i].ObjectId;
                    line.StartPoint = edgesList[i].StartPoint;
                    line.EndPoint = edgesList[i].EndPoint;
                    line.CentrPoint = new Point3d((edgesList[i].StartPoint.X + edgesList[i].EndPoint.X) / 2, (edgesList[i].StartPoint.Y + edgesList[i].EndPoint.Y) / 2, (edgesList[i].StartPoint.Z + edgesList[i].EndPoint.Z) / 2);
                    edgesList2.Add(line);

                }


            }
            return edgesList2;
        }

        public void CreatTextFromEdge(string nameSearchLayer, List<Edge> listineCrearTest, Editor ed, Database dbCurrent, Transaction trAdding)
        {


            // Ищу на какой слой закинуть
            LayerTable acLyrTbl = trAdding.GetObject(dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;
            ObjectId acLyrId = ObjectId.Null;
            if (acLyrTbl.Has(nameSearchLayer))
            {
                acLyrId = acLyrTbl[nameSearchLayer];
            }

            // что б стащить цвет
            LayerTableRecord acLyrTblRec = trAdding.GetObject(acLyrId, OpenMode.ForRead) as LayerTableRecord;

            // Для того что бы закинуть текст
            BlockTable acBlkTbl = trAdding.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;

            BlockTableRecord acBlkTblRec = trAdding.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (Edge element in listineCrearTest)
            {
                // Характеристики текста
                MText acMText = new MText();
                acMText.Color = acLyrTblRec.Color; //Цвето по слою
                acMText.Location = element.CentrPoint;
                acMText.Contents = element.Name.ToString();// +1 для визуализации
                acMText.TextHeight = 4; //Размер шрифта
                acMText.Height = 4; //Высота 
                acMText.LayerId = acLyrId;
                //acMText.Layer = "MyLayer"; // Можно и так на слой
                acMText.Attachment = AttachmentPoint.MiddleCenter; //Центровка текста

                acBlkTblRec.AppendEntity(acMText);
                //trAdding.AddNewlyCreatedDBObject(acMText, true);

            }
        }


    }
}