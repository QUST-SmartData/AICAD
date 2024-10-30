using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Button = System.Windows.Forms.Button;
using TextBox = System.Windows.Forms.TextBox;
using viewPort = Autodesk.AutoCAD.DatabaseServices.Viewport;


// C#

namespace AICAD
{

    // 窗口类
    public partial class Form1 : Form
    {
        //******************************* 窗口初始化 ************************************
        public Form1()
        {
            // 初始化窗口组件（窗口设计模式下自动生成的）
            InitializeComponent();
        }

        //******************************* 按钮功能的实现 ************************************
        // 按钮和函数的绑定在上面的 InitializeComponent() 中设置了
        // 智能分图功能
        // 需要与用户交互，首先选中一个使用的图纸图框对象（以图块的形式存在的），然后框选需要分图的区域，回车确认。
        // 目前实现了基本的分图功能，但是需要结合目标识别网络yolo模型，大概思路如下：
        // 将用户选中的区域对象转为图像，输入到yolo中获取识别的所有路口中心坐标，根据路口中心坐标对应原始选中区域的位置，来确定下面函数中要显示的分割子图的区域中心坐标。
        // 需要解决的问题：
        // 选中的区域对象转为图像？
        // yolo需要返回路口位置的中心坐标，模型是python实现的需要考虑如何调用？
        // 路口调整的策略？只需要把自动分图中路口分的不好的调整一下，子图之间可以有重叠的区域，但是不能遗漏
        private void Split_CAD_Image(object sender, EventArgs e)
        {
            // 最小化窗口
            this.WindowState = FormWindowState.Minimized;

            // 分图操作
            SplitViewport();

            // 还原窗口
            this.WindowState = FormWindowState.Normal;

        }


        // 自动编号功能
        // 需要与用户交互，处理用户选中区域的多个图框对象，依次遍历修改编号
        private void Auto_Numbering(object sender, EventArgs e)
        {
            // 最小化窗口
            this.WindowState = FormWindowState.Minimized;

            // 编号操作
            Numbering_Selected_Frame();

            // 还原窗口
            this.WindowState = FormWindowState.Normal;
        }

        // 文本批量替换功能
        // 需要与用户交互，处理用户选中区域的多个图框对象，依次遍历修改指定或所有属性的文本内容
        private void Text_Batch_Replacement(object sender, EventArgs e)
        {
            // 最小化窗口
            this.WindowState = FormWindowState.Minimized;
            
            // 文本替换操作
            Replacing_Selected_Frame();
            
            // 还原窗口
            this.WindowState = FormWindowState.Normal;
        }

        // 批量打印功能
        // 需要与用户交互，处理用户选中区域的多个对象（每个图框及内部的绘图区域为一个整体），依次遍历进行打印
        private void Batch_Print(object sender, EventArgs e)
        {
            // TODO: 实现批量打印功能
        }


        //**************************************** 功能实现 *********************************************
        // CAD命令绑定（通过在CAD中执行 ShowMainWindow 命令来调用此处的ShowMainWindow函数功能）
        [CommandMethod("ShowMainWindow")]
        public void ShowMainWindow()
        {
            Form1 mainWindow = new Form1();
            acadApp.ShowModelessDialog(mainWindow);
        }

        // CAD命令绑定（通过在CAD中执行 SplitViewport 命令调用此函数的功能）
        [CommandMethod("SplitViewport")]
        public void SplitViewport()
        {
            // 锁定DWG文档，防止用户修改冲突
            DocumentLock docLock = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.LockDocument();
            // 获取打开的活动文档对象
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            // 获取编辑器对象
            Editor ed = doc.Editor;
            // 获取数据库对象
            Database db = doc.Database;

            // 开启事务
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // ********************************************  用户交互，选中图框和绘图区域  ********************************************

                // ----------------- 选择A3图框块 -----------------
                // ####################################### TODO: 需要改进，因为还需要其他不同类型的图框，不该写死！ #######################################
                // 显示提示消息
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择图纸图框块");
                // 设置拒绝消息
                peo.SetRejectMessage("\n只可以选择图块！");
                // 只允许选择图块
                peo.AddAllowedClass(typeof(BlockReference), false);
                // 获取实体选择结果
                PromptEntityResult per = ed.GetEntity(peo);
                // 校验实体选择结果
                if (per.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("未选择图纸图框块！\n");
                    return;
                }
                // 获取选择的图块引用
                BlockReference a3FrameBlock = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (a3FrameBlock == null)
                {
                    ed.WriteMessage("选中的不是图块！\n");
                    return;
                }


                // ----------------- 选择模型空间中的绘图区域 -----------------
                // 提示用户选择模型空间中的绘图区域
                PromptSelectionOptions opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\n选择模型空间中的绘图区域，选中后按Enter完成选择";
                // 获取选择集结果
                PromptSelectionResult selectionResult = ed.GetSelection(opts);
                // 校验选择集结果
                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("未选中对象！\n");
                    return;
                }
                // 获取选择集对象值
                SelectionSet selectionSet = selectionResult.Value;
                
                
                // 转图像
                string tempDirectory = System.IO.Path.GetTempPath();
                // 拼接图像文件名
                string outputPath = System.IO.Path.Combine(tempDirectory, "output.png");
                ed.WriteMessage(outputPath);
                ExportSelectionToImage(doc, selectionSet, outputPath);


                // 预测
                List<Tuple<float, float>> centers = DetectObjects(outputPath);
                // 对中心点列表按照第一个值 (centerX) 进行排序
                centers = centers.OrderBy(center => center.Item1).ToList();

                // 根据上面返回的路口中心点列表，对下面依次平均分割的子图进行调整，
                // 如果子图中有路口且不在图纸中间，就调整视口位置将其放在子图中间，
                // 其后的子图也根前面图的调整来适当调整分割坐标
                CreateSplitLayouts(doc, centers, selectionSet, a3FrameBlock, tr);


                //// ********************************************  等分图  ********************************************
                //// 创建新的布局
                //LayoutManager layoutManager = LayoutManager.Current;
                //// 布局命名
                //string layoutName = "NewLayout-" + DateTime.Now.ToString("yyyyMMddHHmmss"); ;
                //// 创建布局
                //ObjectId layoutId = layoutManager.CreateLayout(layoutName);
                //// 设置创建的新布局为当前活动布局
                //layoutManager.CurrentLayout = layoutName;
                //// 获取可修改的布局对象
                //Layout layout = tr.GetObject(layoutId, OpenMode.ForWrite) as Layout;
                //// 获取可修改的布局对象的图块记录表
                //BlockTableRecord btr = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;

                //// 从布局对象的图块记录表中删除原有的默认视口
                //foreach (ObjectId objId in btr)
                //{
                //    // 获取实体
                //    Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                //    // 如果是视口，就删除
                //    if (ent is viewPort)
                //    {
                //        ent.UpgradeOpen();
                //        ent.Erase();
                //    }
                //}

                //// 循环创建分割的子图
                //// 获取A3图框的外部边界（外部真实尺寸）
                //Extents3d a3FrameOverallExtents = GetOverallExtents(a3FrameBlock);
                //double overallFrameWidth = a3FrameOverallExtents.MaxPoint.X - a3FrameOverallExtents.MinPoint.X;
                //double overallFrameHeight = a3FrameOverallExtents.MaxPoint.Y - a3FrameOverallExtents.MinPoint.Y;

                //// 获取A3图框内部实际绘图区域的边界（内部绘图尺寸）
                //Extents3d a3FrameExtents = GetDrawingAreaExtents(a3FrameBlock);
                //double frameWidth = a3FrameExtents.MaxPoint.X - a3FrameExtents.MinPoint.X;
                //double frameHeight = a3FrameExtents.MaxPoint.Y - a3FrameExtents.MinPoint.Y;

                //// 设置横向和纵向布置多个子图图框的间隔
                //double spacing = 50.0;

                //// 获取选中区域的边界和大小
                //Extents3d selectionExtents = GetSelectionExtents(selectionSet);
                //double selectionWidth = selectionExtents.MaxPoint.X - selectionExtents.MinPoint.X;
                //double selectionHeight = selectionExtents.MaxPoint.Y - selectionExtents.MinPoint.Y;

                //// 计算子图的行列数
                //int columns = (int)Math.Ceiling(selectionWidth / frameWidth);
                //int rows = (int)Math.Ceiling(selectionHeight / frameHeight);

                //// 遍历分图
                //for (int row = 0; row < rows; row++)
                //{
                //    for (int col = 0; col < columns; col++)
                //    {
                //        // 插入A3图框块
                //        // 图框插入点坐标
                //        Point3d frameInsertPoint = new Point3d(col * (overallFrameWidth + spacing), row * (overallFrameHeight + spacing), 0);
                //        // 创建新的图框块引用
                //        BlockReference newA3FrameBlock = new BlockReference(frameInsertPoint, a3FrameBlock.BlockTableRecord);
                //        // 添加到布局对象的图块记录表
                //        btr.AppendEntity(newA3FrameBlock);
                //        // 添加对象到数据库
                //        tr.AddNewlyCreatedDBObject(newA3FrameBlock, true);

                //        // TODO： 通过块引用的属性修改图框文本、序号，可以根据Tag值来设置序号和其他文本内容
                //        // 复制原始块引用的属性到新块引用
                //        foreach (ObjectId id in a3FrameBlock.AttributeCollection)
                //        {
                //            DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                //            if (obj is AttributeReference)
                //            {
                //                AttributeReference attRef = obj as AttributeReference;
                //                AttributeReference newAttRef = attRef.Clone() as AttributeReference;
                //                // 判断属性名称，修改属性值（可能还需要加一些判断）
                //                if (newAttRef.Tag == "第页")
                //                {
                //                    newAttRef.TextString = "" + ((rows - 1 - row) * columns + col + 1);
                //                }
                //                else if (newAttRef.Tag == "总页")
                //                {
                //                    newAttRef.TextString = "" + columns * rows;
                //                }
                //                else if (newAttRef.Tag == "项目名称")
                //                {
                //                    newAttRef.TextString = "某某项目";
                //                }
                //                else if (newAttRef.Tag == "工程名称")
                //                {
                //                    newAttRef.TextString = "某某工程";
                //                }

                //                // 设置位置属性
                //                newAttRef.Position = attRef.Position.TransformBy(newA3FrameBlock.BlockTransform);
                //                if (attRef.Justify != AttachmentPoint.BaseLeft)
                //                {
                //                    newAttRef.AlignmentPoint = attRef.AlignmentPoint.TransformBy(newA3FrameBlock.BlockTransform);
                //                }
                //                newAttRef.Justify = attRef.Justify;
                //                // 更新块数据
                //                newA3FrameBlock.UpgradeOpen();
                //                newA3FrameBlock.AttributeCollection.AppendAttribute(newAttRef);
                //                tr.AddNewlyCreatedDBObject(newAttRef, true);
                //            }
                //        }

                //        // 使用ATTSYNC命令更新块属性，使其能够正确显示文本内容
                //        // 构造ATTSYNC命令
                //        string attSyncCmd = "_.ATTSYNC\n" + "_name\n" + newA3FrameBlock.Name + "\n";
                //        // 调用ATTSYNC命令
                //        doc.SendStringToExecute(attSyncCmd, true, false, false);

                //        // 更新图块实例
                //        tr.TransactionManager.QueueForGraphicsFlush();

                //        //// 在CAD的命令提示中输出属性和值（调试用的）
                //        //// 获取块引用的所有属性
                //        //Dictionary<string, string> attributes = GetBlockAttributes(newA3FrameBlock, tr);
                //        //// 显示属性
                //        //foreach (var attribute in attributes)
                //        //{
                //        //    ed.WriteMessage($"\n{row},{col},属性标签: {attribute.Key}, 属性值: {attribute.Value}");
                //        //}

                //        // 计算要显示的子图区域的中心点在选中区域中的相对位置坐标
                //        double centerX = selectionExtents.MinPoint.X + frameWidth / 2 + col * frameWidth;
                //        double centerY = selectionExtents.MinPoint.Y + frameHeight / 2 + row * frameHeight;

                //        // 计算子图显示区域在图框中的插入点相对位置坐标
                //        Point3d drawingAreaMinPoint = newA3FrameBlock.Position + new Vector3d(30, 58, 0);
                //        Point3d drawingAreaMaxPoint = newA3FrameBlock.Position + new Vector3d(410, 287, 0);
                //        Point3d drawingAreaCenter = new Point3d((drawingAreaMinPoint.X + drawingAreaMaxPoint.X) / 2,
                //                                                (drawingAreaMinPoint.Y + drawingAreaMaxPoint.Y) / 2,
                //                                                0);
                //        // 创建视口：图框中的图纸显示区域的中心位置、宽度、高度，需要显示的图纸的选中区域的子图区域的中心点位置坐标、高度
                //        viewPort viewport = new viewPort
                //        {
                //            CenterPoint = drawingAreaCenter,
                //            Width = frameWidth,
                //            Height = frameHeight,
                //            ViewCenter = new Point2d(centerX, centerY),
                //            ViewHeight = frameHeight
                //        };
                //        btr.AppendEntity(viewport);
                //        tr.AddNewlyCreatedDBObject(viewport, true);

                //        // 启用视口
                //        viewport.On = true;
                //    }
                //}


                // 提交事务
                tr.Commit();

                // 窗口输出提示内容
                ed.WriteMessage("分图完成！\n");

                // 解除文档锁定
                docLock.Dispose();
            }
        }

        private void CreateSplitLayouts(Document doc, List<Tuple<float, float>> centers, SelectionSet selectionSet, BlockReference a3FrameBlock, Transaction tr)
        {
            // 用户输入图纸参数
            // 声明用于存储属性leftMargin和用户输入值的字典
            Dictionary<string, string> atts = new Dictionary<string, string>(){
                { "图框宽度", "420" },
                { "图框高度", "297" },
                { "标题栏高度", "48" },
                { "左边距", "30" },
                { "右边距", "10" },
                { "上边距", "10" },
                { "下边距", "10" },
                { "缩放比例（分图比例/原始比例）", "1" }
            };
            ShowInputDialog(atts);
            double overallFrameWidth = Double.Parse(atts["图框宽度"]);
            double overallFrameHeight = Double.Parse(atts["图框高度"]);
            double titleHeight = Double.Parse(atts["标题栏高度"]);
            double leftMargin = Double.Parse(atts["左边距"]);
            double rightMargin = Double.Parse(atts["右边距"]);
            double topMargin = Double.Parse(atts["上边距"]);
            double bottomMargin = Double.Parse(atts["下边距"]);
            double scaleFactor = Double.Parse(atts["缩放比例（分图比例/原始比例）"]); 


            // 两张相邻子图的重叠区域大小，避免分图导致信息丢失
            double common_area = 10;

            // 获取A3图框的内部实际绘图区域的边界尺寸
            double frameWidth = overallFrameWidth-leftMargin-rightMargin;
            double frameHeight = overallFrameHeight-topMargin-bottomMargin-titleHeight;
            // 获取A3图框的内部实际绘图区域的边界，带缩放因子的
            double scaleFrameWidth = frameWidth / scaleFactor - common_area;
            double scaleFrameHeight = frameHeight / scaleFactor - common_area;

            // 设置横向和纵向布置多个子图图框的间隔
            double spacing = 50.0;

            // 创建新的布局
            LayoutManager layoutManager = LayoutManager.Current;
            // 布局命名
            string layoutName = "NewLayout-" + DateTime.Now.ToString("yyyyMMddHHmmss"); ;
            // 创建布局
            ObjectId layoutId = layoutManager.CreateLayout(layoutName);
            // 设置创建的新布局为当前活动布局
            layoutManager.CurrentLayout = layoutName;
            // 获取可修改的布局对象
            Layout layout = tr.GetObject(layoutId, OpenMode.ForWrite) as Layout;
            // 获取可修改的布局对象的图块记录表
            BlockTableRecord btr = tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;

            // 从布局对象的图块记录表中删除原有的默认视口
            foreach (ObjectId objId in btr)
            {
                // 获取实体
                Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                // 如果是视口，就删除
                if (ent is viewPort)
                {
                    ent.UpgradeOpen();
                    ent.Erase();
                }
            }


            // 获取选中区域的边界坐标
            Extents3d selectionExtents = GetSelectionExtents(selectionSet);

            // 获取选中区域的边界和大小
            double selectionWidth = selectionExtents.MaxPoint.X - selectionExtents.MinPoint.X;
            double selectionHeight = selectionExtents.MaxPoint.Y - selectionExtents.MinPoint.Y;

            // 如果高度大于宽度，则旋转选中区域
            // 检查高度是否大于宽度
            if (selectionHeight > selectionWidth)
            {
                // 计算旋转中心
                Point3d centerPoint = new Point3d(
                    (selectionExtents.MinPoint.X + selectionExtents.MaxPoint.X) / 2,
                    (selectionExtents.MinPoint.Y + selectionExtents.MaxPoint.Y) / 2,
                    0);

                // 创建旋转矩阵（90度）
                Matrix3d rotationMatrix = Matrix3d.Rotation(Math.PI / 2, Vector3d.ZAxis, centerPoint);

                // 遍历选择集中的对象进行旋转
                foreach (ObjectId objId in selectionSet.GetObjectIds())
                {
                    // 打开对象进行修改
                    Entity entity = (Entity)tr.GetObject(objId, OpenMode.ForWrite);
                    // 应用旋转矩阵
                    entity.TransformBy(rotationMatrix);
                }

                double temp = selectionWidth;
                selectionWidth = selectionHeight;
                selectionHeight = temp;

                // 提交事务
                tr.Commit();
            }
                        

            // 计算子图的行列数
            int columns = (int)Math.Ceiling(selectionWidth / scaleFrameWidth);
            int rows = (int)Math.Ceiling(selectionHeight / scaleFrameHeight);

            // 分图中心点偏移和索引
            double offset_x = 0;
            double offset_y = 0;
            int center_index = 0;

            // 分图中心点
            List<Tuple<double, double>> spilit_centers = new List<Tuple<double, double>>();

            // 遍历确定分图中心点
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {

                    // 计算要显示的子图区域的中心点在选中区域中的相对位置坐标
                    double offset_centerX = scaleFrameWidth / 2 + col * scaleFrameWidth + offset_x;
                    double offset_centerY = scaleFrameHeight / 2 + row * scaleFrameHeight + offset_y;
                    double centerX = selectionExtents.MinPoint.X + offset_centerX;
                    double centerY = selectionExtents.MinPoint.Y + offset_centerY;

                    if (center_index < centers.Count && Math.Abs(offset_centerX - centers[center_index].Item1) <= scaleFrameWidth / 2 && Math.Abs(offset_centerY - centers[center_index].Item2) <= scaleFrameHeight / 2)
                    {
                        offset_x = centers[center_index].Item1 - offset_centerX;
                        offset_y = centers[center_index].Item2 - offset_centerY;
                        center_index++;
                        centerX += offset_x;
                        centerY += offset_y;
                    }

                    // 累计的偏移导致相邻两分图中心点距离超过一个图框尺寸，需要在中间补一个子图
                    if (spilit_centers.Count > 0)
                    {
                        if (centerX - spilit_centers.Last().Item1 > scaleFrameWidth || centerY - spilit_centers.Last().Item2 > scaleFrameHeight)
                        {
                            spilit_centers.Add(new Tuple<double, double>(spilit_centers.Last().Item1 + (centerX - spilit_centers.Last().Item1)/2, spilit_centers.Last().Item2 + (centerY - spilit_centers.Last().Item2)/2));
                        }
                    }

                    spilit_centers.Add(new Tuple<double, double>(centerX, centerY));
                                                            
                }
            }

            // 最后一个分图中心点没有完全覆盖到选中区域的结尾，需要在最后补一个子图
            if (spilit_centers.Count > 0)
            {
                if (spilit_centers.Last().Item1 + scaleFrameWidth / 2 < selectionExtents.MaxPoint.X || spilit_centers.Last().Item2 + scaleFrameHeight / 2 < selectionExtents.MaxPoint.Y)
                {
                    spilit_centers.Add(new Tuple<double, double>(spilit_centers.Last().Item1 + (selectionExtents.MaxPoint.X - spilit_centers.Last().Item1) / 2, spilit_centers.Last().Item2 + (selectionExtents.MaxPoint.Y - spilit_centers.Last().Item2) / 2));
                }
            }

            
            // 遍历分图中心点，分割子图
            for (int i = 0; i < spilit_centers.Count; i++)
            {
                // 插入A3图框块
                // 图框插入点坐标
                Point3d frameInsertPoint = new Point3d(i * (overallFrameWidth + spacing), 0, 0);
                // 创建新的图框块引用
                BlockReference newA3FrameBlock = new BlockReference(frameInsertPoint, a3FrameBlock.BlockTableRecord);
                // 添加到布局对象的图块记录表
                btr.AppendEntity(newA3FrameBlock);
                // 添加对象到数据库
                tr.AddNewlyCreatedDBObject(newA3FrameBlock, true);

                // 复制原始块引用的属性到新块引用
                foreach (ObjectId id in a3FrameBlock.AttributeCollection)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                    if (obj is AttributeReference)
                    {
                        AttributeReference attRef = obj as AttributeReference;
                        AttributeReference newAttRef = attRef.Clone() as AttributeReference;
                        // 判断属性名称，修改属性值
                        if (newAttRef.Tag == "第页")
                        {
                            newAttRef.TextString = "" + (i + 1);
                        }
                        else if (newAttRef.Tag == "总页")
                        {
                            newAttRef.TextString = "" + spilit_centers.Count;
                        }
                        
                        // 设置位置属性
                        newAttRef.Position = attRef.Position.TransformBy(newA3FrameBlock.BlockTransform);
                        if (attRef.Justify != AttachmentPoint.BaseLeft)
                        {
                            newAttRef.AlignmentPoint = attRef.AlignmentPoint.TransformBy(newA3FrameBlock.BlockTransform);
                        }
                        newAttRef.Justify = attRef.Justify;
                        // 更新块数据
                        newA3FrameBlock.UpgradeOpen();
                        newA3FrameBlock.AttributeCollection.AppendAttribute(newAttRef);
                        tr.AddNewlyCreatedDBObject(newAttRef, true);
                    }
                }

                // 使用ATTSYNC命令更新块属性，使其能够正确显示文本内容
                // 构造ATTSYNC命令
                string attSyncCmd = "_.ATTSYNC\n" + "_name\n" + newA3FrameBlock.Name + "\n";
                // 调用ATTSYNC命令
                doc.SendStringToExecute(attSyncCmd, true, false, false);

                // 更新图块实例
                tr.TransactionManager.QueueForGraphicsFlush();

                // 计算子图显示区域在图框中的插入点相对位置坐标                              
                Point3d drawingAreaMinPoint = newA3FrameBlock.Position + new Vector3d(leftMargin, bottomMargin+titleHeight, 0.0);
                Point3d drawingAreaMaxPoint = newA3FrameBlock.Position + new Vector3d(overallFrameWidth-rightMargin, overallFrameHeight-topMargin, 0.0);
                Point3d drawingAreaCenter = new Point3d((drawingAreaMinPoint.X + drawingAreaMaxPoint.X) / 2,
                                                        (drawingAreaMinPoint.Y + drawingAreaMaxPoint.Y) / 2,
                                                        0);

                // 创建视口：图框中的图纸显示区域的中心位置、宽度、高度，需要显示的图纸的选中区域的子图区域的中心点位置坐标、高度
                viewPort viewport = new viewPort
                {
                    CenterPoint = drawingAreaCenter,
                    Width = frameWidth,
                    Height = frameHeight,
                    ViewCenter = new Point2d(spilit_centers[i].Item1, spilit_centers[i].Item2),
                    CustomScale = scaleFactor,
                };
                
                btr.AppendEntity(viewport);
                tr.AddNewlyCreatedDBObject(viewport, true);

                // 启用视口
                viewport.On = true;
            }

        }

  

        private void Numbering_Selected_Frame()
        {
            // 锁定DWG文档，防止用户修改冲突
            DocumentLock docLock = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.LockDocument();
            // 获取打开的活动文档对象
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            // 获取编辑器对象
            Editor ed = doc.Editor;
            // 获取数据库对象
            Database db = doc.Database;

            // 开启事务
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // ----------------- 选择所有图框块 -----------------
                // 提示用户选择所有图框块
                PromptSelectionOptions opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\n选择所有图框图块，选中后按Enter完成选择";

                // 获取选择集结果
                PromptSelectionResult selectionResult = ed.GetSelection(opts);
                // 校验选择集结果
                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("未选中对象！\n");
                    return;
                }

                // 获取选择集对象值
                SelectionSet selectionSet = selectionResult.Value;

                // ----------------- 对图框块进行排序和编号 -----------------
                List<BlockReference> frameBlocks = new List<BlockReference>();
                foreach (ObjectId id in selectionSet.GetObjectIds())
                {
                    BlockReference block = tr.GetObject(id, OpenMode.ForWrite) as BlockReference;
                    if (block != null)
                    {
                        frameBlocks.Add(block);
                    }
                }

                // 按照从左到右，从上到下排序
                var sortedBlocks = frameBlocks.OrderBy(b => b.Position.Y).ThenBy(b => b.Position.X).ToList();

                // 编号
                for (int i = 0; i < sortedBlocks.Count; i++)
                {
                    ed.WriteMessage($"图框块 {i + 1}: {sortedBlocks[i].Name} at {sortedBlocks[i].Position}\n");

                    foreach (ObjectId id in sortedBlocks[i].AttributeCollection)
                    {
                        DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                        if (obj is AttributeReference)
                        {
                            AttributeReference attRef = obj as AttributeReference;
                            // 判断属性名称，修改属性值（可能还需要加一些判断）
                            if (attRef.Tag == "第页")
                            {
                                attRef.TextString = "" + (i + 1);
                            }
                            else if (attRef.Tag == "总页")
                            {
                                attRef.TextString = "" + sortedBlocks.Count;
                            }
                        }
                    }
                }

                // 提交事务
                tr.Commit();
                // 窗口输出提示内容
                ed.WriteMessage("文本替换完成！\n");
                // 解除文档锁定
                docLock.Dispose();
            }

        }


        private void Replacing_Selected_Frame()
        {
            // 锁定DWG文档，防止用户修改冲突
            DocumentLock docLock = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.LockDocument();
            // 获取打开的活动文档对象
            Document doc = acadApp.DocumentManager.MdiActiveDocument;
            // 获取编辑器对象
            Editor ed = doc.Editor;
            // 获取数据库对象
            Database db = doc.Database;

            // 开启事务
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // ----------------- 选择所有图框块 -----------------
                // 提示用户选择所有图框块
                PromptSelectionOptions opts = new PromptSelectionOptions();
                opts.MessageForAdding = "\n选择所有图框图块，选中后按Enter完成选择";

                // 获取选择集结果
                PromptSelectionResult selectionResult = ed.GetSelection(opts);
                // 校验选择集结果
                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("未选中对象！\n");
                    return;
                }

                // 获取选择集对象值
                SelectionSet selectionSet = selectionResult.Value;

                // ----------------- 对图框块进行排序和编号 -----------------
                List<BlockReference> frameBlocks = new List<BlockReference>();
                foreach (ObjectId id in selectionSet.GetObjectIds())
                {
                    BlockReference block = tr.GetObject(id, OpenMode.ForWrite) as BlockReference;
                    if (block != null)
                    {
                        frameBlocks.Add(block);
                    }
                }

                // 按照从左到右，从上到下排序
                var sortedBlocks = frameBlocks.OrderBy(b => b.Position.Y).ThenBy(b => b.Position.X).ToList();

                // 声明用于存储属性 Tag 和用户输入值的字典
                Dictionary<string, string> attRefTags = new Dictionary<string, string>();

                // 编号
                for (int i = 0; i < sortedBlocks.Count; i++)
                {
                    if (i == 0)
                    {
                        // 获取所有属性 Tag 和其对应值
                        foreach (ObjectId id in sortedBlocks[i].AttributeCollection)
                        {
                            DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                            if (obj is AttributeReference attRef)
                            {
                                if (attRef.Tag != "图纸名称1" && attRef.Tag != "第页")
                                {
                                    attRefTags[attRef.Tag] = attRef.TextString; // 存储 Tag 和其当前值
                                }
                                
                            }
                        }

                        // 在 UI 窗口显示 Tag 和对应输入框，获取用户输入
                        ShowInputDialog(attRefTags);
                    }

                    // 使用用户输入为对应的 Tag 赋值
                    foreach (ObjectId id in sortedBlocks[i].AttributeCollection)
                    {
                        DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                        if (obj is AttributeReference attRef)
                        {
                            // 根据属性名称，修改属性值
                            if (attRefTags.TryGetValue(attRef.Tag, out string newValue))
                            {
                                attRef.TextString = newValue; // 设置新值
                            }
                        }
                    }
                }

                // 提交事务
                tr.Commit();
                // 窗口输出提示内容
                ed.WriteMessage("编号完成！\n");
                // 解除文档锁定
                docLock.Dispose();
            }
        }


        // 创建输入对话框，显示 Tag 和对应值的输入框
        private void ShowInputDialog(Dictionary<string, string> attRefTags)
        {
            Form inputForm = new Form
            {
                Text = "输入属性值",
                Width = 400,
                Height = 400,
                StartPosition = FormStartPosition.CenterScreen
            };

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = attRefTags.Count + 1, // +1 for the button row
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            // 设置列的比例
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 标签列自适应
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // 输入框列占据剩余空间

            inputForm.Controls.Add(panel);

            TextBox[] textBoxes = new TextBox[attRefTags.Count];
            int row = 0;

            foreach (var kvp in attRefTags)
            {
                Label label = new Label
                {
                    Text = kvp.Key,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right
                };
                panel.Controls.Add(label, 0, row);

                TextBox textBox = new TextBox
                {
                    Text = kvp.Value,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right // 使输入框在宽度变化时保持对齐
                };
                panel.Controls.Add(textBox, 1, row);
                textBoxes[row] = textBox;
                row++;
            }

            // 添加确认按钮
            Button confirmButton = new Button { Text = "确认", Dock = DockStyle.Bottom };
            confirmButton.Click += (sender, e) =>
            {
                for (int i = 0; i < textBoxes.Length; i++)
                {
                    attRefTags[attRefTags.Keys.ElementAt(i)] = textBoxes[i].Text; // 更新字典中的值
                }
                inputForm.DialogResult = DialogResult.OK;
                inputForm.Close();
            };

            panel.Controls.Add(confirmButton, 0, row); // 在最后一行添加按钮
            panel.SetColumnSpan(confirmButton, 2); // 按钮跨越两列

            inputForm.ShowDialog();
        }

        //private void ShowInputDialog(Dictionary<string, string> attRefTags)
        //{
        //    Form inputForm = new Form();
        //    inputForm.Text = "输入属性值";
        //    inputForm.Width = 400;
        //    inputForm.Height = 450;
        //    inputForm.StartPosition = FormStartPosition.CenterScreen;

        //    TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill };
        //    inputForm.Controls.Add(panel);

        //    TextBox[] textBoxes = new TextBox[attRefTags.Count];
        //    int row = 0;

        //    foreach (var kvp in attRefTags)
        //    {
        //        Label label = new Label { Text = kvp.Key, AutoSize = true };
        //        panel.Controls.Add(label, 0, row);

        //        TextBox textBox = new TextBox { Text = kvp.Value, Width = 300 };
        //        panel.Controls.Add(textBox, 1, row);
        //        textBoxes[row] = textBox;
        //        row++;
        //    }

        //    Button confirmButton = new Button { Text = "确认", Dock = DockStyle.Bottom };
        //    confirmButton.Click += (sender, e) =>
        //    {
        //        for (int i = 0; i < textBoxes.Length; i++)
        //        {
        //            attRefTags[attRefTags.Keys.ElementAt(i)] = textBoxes[i].Text; // 更新字典中的值
        //        }
        //        inputForm.DialogResult = DialogResult.OK;
        //        inputForm.Close();
        //    };

        //    inputForm.Controls.Add(confirmButton);
        //    inputForm.ShowDialog();
        //}


        private void ExportSelectionToImage(Document doc, SelectionSet selection, string outputPath)
        {

            Editor ed = doc.Editor;
            // 获取数据库对象
            Database db = doc.Database;

            // 开启事务
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {

                // 获取选择对象的边界
                Extents3d extents = GetSelectionExtents(selection);

                double cadWidth = extents.MaxPoint.X - extents.MinPoint.X;
                double cadHeight = extents.MaxPoint.Y - extents.MinPoint.Y;

                // 定义图像尺寸
                int imageWidth = (int)Math.Ceiling(cadWidth);  // 图像宽度
                int imageHeight = (int)Math.Ceiling(cadHeight); // 图像高度


                using (Bitmap bmp = new Bitmap(imageWidth, imageHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {

                        // 设置背景色
                        g.Clear(Color.FromArgb(32, 38, 46));    // RGB 20262e

                        g.SmoothingMode = SmoothingMode.AntiAlias;


                        // 平移到中心
                        double centerX = (extents.MaxPoint.X + extents.MinPoint.X) / 2;
                        double centerY = (extents.MaxPoint.Y + extents.MinPoint.Y) / 2;

                        // 平移并缩放至图像坐标
                        Matrix transform = new Matrix();
                        transform.Translate((float)(imageWidth / 2), (float)(imageHeight / 2)); // 将中心移到图像中心
                        transform.Translate(-(float)centerX, -(float)centerY); // 平移到中心位置

                        // 应用转换矩阵
                        g.Transform = transform;

                        // 遍历选择集并绘制
                        foreach (SelectedObject selObj in selection)
                        {
                            if (selObj != null)
                            {
                                Entity entity = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForWrite);
                                DrawEntity(g, entity);
                            }
                        }
                    }

                    // 保存图像
                    bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    //ed.WriteMessage($"\n图像已导出为: {outputPath}");
                }
            }
        }

        // 绘制CAD实体到图像
        private void DrawEntity(Graphics g, Entity entity)
        {
            Pen pen = new Pen(Color.Black); // 默认颜色为黑色

            if (entity is Line line)
            {
                // 获取颜色
                pen.Color = GetEntityColor(line);
                // 绘制线条
                g.DrawLine(pen,
                    new PointF((float)line.StartPoint.X, (float)line.StartPoint.Y),
                    new PointF((float)line.EndPoint.X, (float)line.EndPoint.Y));
            }
            else if (entity is Circle circle)
            {
                // 获取颜色
                pen.Color = GetEntityColor(circle);
                // 绘制圆
                float centerX = (float)circle.Center.X;
                float centerY = (float)circle.Center.Y;
                float radius = (float)circle.Radius;

                g.DrawEllipse(pen, centerX - radius, centerY - radius, 2 * radius, 2 * radius);
            }
            else if (entity is Polyline polyline)
            {
                // 获取颜色
                pen.Color = GetEntityColor(polyline);
                // 绘制多段线
                List<PointF> points = new List<PointF>();
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point2d pt = polyline.GetPoint2dAt(i);
                    points.Add(new PointF((float)pt.X, (float)pt.Y));
                }

                if (polyline.Closed)
                {
                    g.DrawPolygon(pen, points.ToArray());
                }
                else
                {
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        g.DrawLine(pen, points[i], points[i + 1]);
                    }
                }
            }
            else if (entity is Arc arc)
            {
                // 获取颜色
                pen.Color = GetEntityColor(arc);
                // 绘制弧线
                float centerX = (float)arc.Center.X;
                float centerY = (float)arc.Center.Y;
                float radius = (float)arc.Radius;
                float startAngle = (float)arc.StartAngle;
                float endAngle = (float)arc.EndAngle;

                // Convert radians to degrees
                startAngle = (float)(startAngle * 180 / Math.PI);
                endAngle = (float)(endAngle * 180 / Math.PI);

                g.DrawArc(pen, centerX - radius, centerY - radius, 2 * radius, 2 * radius, startAngle, endAngle - startAngle);
            }

        }

        // 获取实体颜色
        private Color GetEntityColor(Entity entity)
        {
            // 获取 AutoCAD 实体的颜色属性
            // 使用 AutoCAD 的颜色对象来设置颜色
            Color color = Color.Black; // 默认颜色为黑色

            if (entity is Line line)
            {
                color = GetAutoCADColor(line.Color);
            }
            else if (entity is Circle circle)
            {
                color = GetAutoCADColor(circle.Color);
            }
            else if (entity is Polyline polyline)
            {
                color = GetAutoCADColor(polyline.Color);
            }
            else if (entity is Arc arc)
            {
                color = GetAutoCADColor(arc.Color);
            }

            return color;
        }

        private Color GetAutoCADColor(Autodesk.AutoCAD.Colors.Color acColor)
        {
            // 将 AutoCAD 颜色对象转换为 .NET Color 对象
            return Color.FromArgb(acColor.ColorValue.R, acColor.ColorValue.G, acColor.ColorValue.B);
        }


        private string SendImageToFlaskAsync(string imagePath)
        {
            using (HttpClient client = new HttpClient())
            {
                using (MultipartFormDataContent content = new MultipartFormDataContent())
                {
                    // 读取图像文件
                    var imageFileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                    var imageContent = new StreamContent(imageFileStream);
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

                    // 添加图像文件到请求内容中
                    content.Add(imageContent, "image", "output.png");

                    // 发送 POST 请求到 Flask 服务器
                    HttpResponseMessage response = client.PostAsync("http://127.0.0.1:5000/detect", content).GetAwaiter().GetResult();

                    // 确保请求成功
                    response.EnsureSuccessStatusCode();

                    // 读取响应内容
                    string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();


                    return responseContent;
                }
            }
        }

        // 示例用法
        private List<Tuple<float, float>> DetectObjects(string imagePath)
        {

            string result = SendImageToFlaskAsync(imagePath);

            // 输出识别结果
            Debug.WriteLine(result);

            // 调用手动解析函数
            DetectionResponse response = DetectionResponse.Parse(result);

            // 存储中心点坐标的列表
            List<Tuple<float, float>> centers = new List<Tuple<float, float>>();

            // 遍历每一个检测结果，输出并计算中心点坐标
            foreach (var detection in response.Detections)
            {
                //Debug.WriteLine($"Category: {detection.Category}");
                //Debug.WriteLine($"Confidence: {detection.Confidence}");
                Debug.WriteLine($"Bounding Box: ({detection.X1}, {detection.Y1}) to ({detection.X2}, {detection.Y2})");

                // 计算中心点坐标
                float centerX = (detection.X1 + detection.X2) / 2;
                float centerY = (detection.Y1 + detection.Y2) / 2;

                // 输出中心点坐标
                //Debug.WriteLine($"Center: ({centerX}, {centerY})");

                // 将中心点坐标加入到列表中
                centers.Add(new Tuple<float, float>(centerX, centerY));
            }

            return centers;
        }


        private Dictionary<string, string> GetBlockAttributes(BlockReference blockRef, Transaction tr)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            foreach (ObjectId id in blockRef.AttributeCollection)
            {
                AttributeReference attRef = tr.GetObject(id, OpenMode.ForWrite) as AttributeReference;
                if (attRef != null)
                {
                    attributes.Add(attRef.Tag, attRef.TextString);
                }
            }

            return attributes;
        }

        // 获取选中区域的边界和大小
        private Extents3d GetSelectionExtents(SelectionSet selectionSet)
        {
            Extents3d extents = new Extents3d();
            foreach (SelectedObject obj in selectionSet)
            {
                Entity entity = obj.ObjectId.GetObject(OpenMode.ForWrite) as Entity;
                if (entity != null)
                {
                    extents.AddExtents(entity.GeometricExtents);
                }
            }
            return extents;
        }

    }
}
