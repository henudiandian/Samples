﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Xml;
//****Gvitech.CityMaker****
using Gvitech.CityMaker.RenderControl;
using Gvitech.CityMaker.FdeCore;
using Gvitech.CityMaker.Math;
using Gvitech.CityMaker.Common;

namespace ImportModel
{
    public partial class MainForm : Form
    {
        private string strMediaPath = System.IO.Path.GetTempPath() + "Gvitech\\SampleMedia";
        private List<string> geoNames = null;  //记录第1个FeatureClass对应的空间列名

        private IFeatureDataSet dataset = null;  // 存储为全局变量，以便获取其对应的逻辑图层树
        private List<IFeatureLayer> layerList = null;  //存储全局变量dataset中已加载的FeatureLayer，以便设置其组可见性
        private IFeatureClass fc = null;  //记录dataset中第1个FeatureClass

        private TreeNode root = null; //存储树根节点   
        private Hashtable nodekeyMap = null;  //text, key存储树节点文本及对应key值

        private TreeNode selectNode = null;  //标记treeView控件中当前被选中的节点

        private System.Guid rootId = new System.Guid();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, System.EventArgs e)
        {


            // 初始化RenderControl控件
            IPropertySet ps = new PropertySet();
            ps.SetProperty("RenderSystem", gviRenderSystem.gviRenderOpenGL);
            this.axRenderControl1.Initialize(true, ps);

            rootId = this.axRenderControl1.ObjectManager.GetProjectTree().RootID;

            // 设置天空盒
            
            if(System.IO.Directory.Exists(strMediaPath))
            {
                string tmpSkyboxPath = strMediaPath + @"\skybox";
                ISkyBox skybox = this.axRenderControl1.ObjectManager.GetSkyBox(0);
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageBack, tmpSkyboxPath + "\\1_BK.jpg");
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageBottom, tmpSkyboxPath + "\\1_DN.jpg");
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageFront, tmpSkyboxPath + "\\1_FR.jpg");
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageLeft, tmpSkyboxPath + "\\1_LF.jpg");
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageRight, tmpSkyboxPath + "\\1_RT.jpg");
                skybox.SetImagePath(gviSkyboxImageIndex.gviSkyboxImageTop, tmpSkyboxPath + "\\1_UP.jpg");   
            }
            else
            {
                MessageBox.Show("请不要随意更改SDK目录名");
                return;
            }

            // 加载FDB
            try
            {
                IConnectionInfo ci = new ConnectionInfo();
                ci.ConnectionType = gviConnectionType.gviConnectionFireBird2x;
                string tmpFDBPath = (strMediaPath + @"\empty.FDB");
                ci.Database = tmpFDBPath;
                IDataSourceFactory dsFactory = new DataSourceFactory();
                IDataSource ds = dsFactory.OpenDataSource(ci);

                TreeNode sourceNode = new TreeNode(ci.Database);
                this.treeViewCatalogTree.Nodes.Add(sourceNode);

                // 获取第1个dataset
                string[] setnames = (string[])ds.GetFeatureDatasetNames();
                if (setnames.Length == 0)
                    return;
                dataset = ds.OpenFeatureDataset(setnames[0]);

                TreeNode setNode = new TreeNode(setnames[0]);
                sourceNode.Nodes.Add(setNode);

                // 获取第1个featureclass
                string[] fcnames = (string[])dataset.GetNamesByType(gviDataSetType.gviDataSetFeatureClassTable);
                if (fcnames.Length == 0)
                    return;
                fc = dataset.OpenFeatureClass(fcnames[0]);

                TreeNode fcNode = new TreeNode(fcnames[0]);
                setNode.Nodes.Add(fcNode);

                // 找到空间列字段
                geoNames = new List<string>();
                IFieldInfoCollection fieldinfos = fc.GetFields();
                for (int i = 0; i < fieldinfos.Count; i++)
                {
                    IFieldInfo fieldinfo = fieldinfos.Get(i);
                    if (null == fieldinfo)
                        continue;
                    IGeometryDef geometryDef = fieldinfo.GeometryDef;
                    if (null == geometryDef)
                        continue;
                    geoNames.Add(fieldinfo.Name);
                }
            }
            catch (COMException ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                return;
            }

            // CreateFeautureLayer 
            layerList = new List<IFeatureLayer>();
            foreach (string geoName in geoNames)
            {
                //****特别注意****
                ISimpleGeometryRender geoRender = new SimpleGeometryRender();
                geoRender.RenderGroupField = "Groupid";
                //****************
                IFeatureLayer featureLayer = this.axRenderControl1.ObjectManager.CreateFeatureLayer(
                fc, geoName, null, geoRender, rootId);
                layerList.Add(featureLayer);

                // 设置featureLayer可见
                SetGroupVisiable(dataset, featureLayer);

                IFieldInfoCollection fieldinfos = fc.GetFields();
                IFieldInfo fieldinfo = fieldinfos.Get(fieldinfos.IndexOf(geoName));
                IGeometryDef geometryDef = fieldinfo.GeometryDef;
                IEnvelope env = geometryDef.Envelope;
                if (env == null || (env.MaxX == 0.0 && env.MaxY == 0.0 && env.MaxZ == 0.0 &&
                    env.MinX == 0.0 && env.MinY == 0.0 && env.MinZ == 0.0))
                    continue;
                IEulerAngle angle = new EulerAngle();
                angle.Set(0, -20, 0);
                this.axRenderControl1.Camera.LookAt(env.Center, 1000, angle);
            }
            this.treeViewCatalogTree.ExpandAll();


            // 加载FeatureDataSet对应的LogicGroupTree到界面控件
            nodekeyMap = new Hashtable();
            byte[] bb = GetLogicTreeContent(dataset);
            if (bb != null)
            {
                string strContent = System.Text.Encoding.UTF8.GetString(bb).Trim();
                ShowCurDataSet(strContent);
            }
            // 删除回收站及其子节点
            foreach (TreeNode node in this.treeViewLogicTree.Nodes[0].Nodes)
            {
                if (node.Text.Equals("回收站"))
                    this.treeViewLogicTree.Nodes.Remove(node);
            }

            {
                this.helpProvider1.SetShowHelp(this.axRenderControl1, true);
                this.helpProvider1.SetHelpString(this.axRenderControl1, "");
                this.helpProvider1.HelpNamespace = "ImportModel.html";
            }

            CommonEntity.RenderEntity = this.axRenderControl1;
            CommonEntity.FormEntity = this;
        }

        #region 读数据库获取逻辑树xml内容
        private byte[] GetLogicTreeContent(IFeatureDataSet dataset)
        {
            byte[] strContent = null;

            try
            {
                IQueryDef qd = dataset.DataSource.CreateQueryDef();
                qd.AddSubField("content");

                qd.Tables = new String[] { "cm_logictree", "cm_group" };
                qd.WhereClause = String.Format("cm_group.groupuid = cm_logictree.groupid "
                                + " and cm_group.DataSet = '{0}'", dataset.Name);

                IFdeCursor cursor = qd.Execute(false);
                IRowBuffer row = null;
                if ((row = cursor.NextRow()) != null)
                {
                    //content
                    int nPose = row.FieldIndex("content");
                    if (nPose != -1)
                    {
                        IBinaryBuffer bb = row.GetValue(nPose) as IBinaryBuffer;
                        strContent = (byte[])bb.AsByteArray();
                    }
                }
            }
            catch (COMException ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                return null;
            }

            return strContent;
        }
        #endregion

        #region 解析逻辑树xml设置FeatureLayer可见
        private void SetGroupVisiable(IFeatureDataSet dataset, IFeatureLayer featureLayer)
        {
            byte[] bb = GetLogicTreeContent(dataset);
            if (bb == null)
                return;
            MemoryStream ms = new MemoryStream(bb);
            XmlDocument doc = new XmlDocument();
            doc.Load(ms);
            XmlNode rootNode = doc.DocumentElement;
            if (rootNode != null && rootNode.HasChildNodes)
            {
                TravelXML(rootNode.ChildNodes[0], featureLayer);
            }
        }

        private void TravelXML(XmlNode pNode, IFeatureLayer fLayer)
        {
            if (pNode == null)
                return;
            string nodeName = pNode.Name;
            if (nodeName.ToLower() == "pgroup" || nodeName.ToLower() == "agroup")
            {
                XmlAttribute att = pNode.Attributes["ID"];
                if (att != null)
                {
                    int grpId;
                    if (int.TryParse(att.Value, out grpId) && fLayer != null)
                    {
                        fLayer.SetGroupVisibleMask(grpId, gviViewportMask.gviViewAllNormalView);
                    }
                }
            }
            else if (nodeName.ToLower() == "pgroups" || nodeName.ToLower() == "agroups")
            {
                foreach (XmlNode node in pNode.ChildNodes)
                {
                    TravelXML(node, fLayer);
                }
            }
        }
        #endregion

        #region 解析逻辑树xml加载到界面控件
        private void ShowCurDataSet(String lc)
        {
            try
            {
                byte[] buf = System.Text.Encoding.UTF8.GetBytes(lc);
                MemoryStream ms = new MemoryStream(buf);
                XmlReaderSettings setting = new XmlReaderSettings();
                setting.IgnoreWhitespace = true;
                XmlReader xr = XmlReader.Create(ms, setting);
                if (xr.EOF)
                    MessageBox.Show("Xml为空");
                xr.Read();
                if (xr.NodeType == XmlNodeType.XmlDeclaration)
                    xr.Read();

                LoadXml2TreeList(xr);
                xr.Close();

                this.treeViewLogicTree.ExpandAll();

                // 只有叶子节点允许改变勾选状态，将组节点用灰色表示出来
                SetNodeForeColor(root);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 从xml导入逻辑图层树
        /// </summary>
        /// <param name="xr">xmlReader对象</param>
        private void LoadXml2TreeList(XmlReader xr)
        {
            int id = 0;
            int parentID = 0;
            String type = null;
            String name = null;
            while (xr.Read())
            {
                if (xr.NodeType == XmlNodeType.Element)
                {
                    type = xr.Name;
                    if (!int.TryParse(xr.GetAttribute("ID"), out id))
                    {
                        return;
                    }
                    name = xr.GetAttribute("Name");
                    if (!int.TryParse(xr.GetAttribute("ParentID"), out parentID))
                    {
                        return;
                    }
                    if (this.treeViewLogicTree.Nodes.Count == 0)
                    {
                        this.treeViewLogicTree.Nodes.Add(id.ToString(), name, 2, 2);  //创建具有指定键、文本和图像的树节点
                        nodekeyMap.Add(name, id);

                        root = this.treeViewLogicTree.Nodes[0];
                        root.Checked = true;  //默认节点都勾选
                    }
                    else
                    {

                        TreeNode pNode = this.treeViewLogicTree.Nodes.Find(parentID.ToString(), true)[0];
                        if (pNode != null)
                        {
                            if (type.Equals("PGroups") || type.Equals("AGroups"))
                                pNode.Nodes.Add(id.ToString(), name, 0, 0);
                            else
                            {
                                pNode.Nodes.Add(id.ToString(), name, 1, 1);
                                pNode.Nodes.Find(id.ToString(), false)[0].ContextMenuStrip = this.contextMenuStrip1;
                            }
                            nodekeyMap.Add(name, id);

                            TreeNode node = pNode.Nodes.Find(id.ToString(), false)[0];
                            node.Checked = true;
                        }
                    }

                    LoadXml2TreeList(xr);
                }
            }
        }

        private void SetNodeForeColor(TreeNode tn)
        {
            if (tn == null) return;
            if (tn.ImageIndex == 0 || tn.ImageIndex == 2)
                tn.ForeColor = Color.Gray;
            else
                tn.ForeColor = Color.Blue;

            foreach (TreeNode tnChild in tn.Nodes)
            {
                SetNodeForeColor(tnChild);
            }
        }
        #endregion
        
        private void treeViewLogicTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {
                // 检查是否是组节点
                if (e.Node.ForeColor == Color.Gray)
                {
                    e.Node.Checked = true;
                    return;
                }

                foreach (IFeatureLayer layer in layerList)
                {
                    layer.SetGroupVisibleMask((int)nodekeyMap[e.Node.Text], e.Node.Checked ? gviViewportMask.gviViewAllNormalView : gviViewportMask.gviViewNone);
                }
            }
        }

        private void treeViewLogicTree_MouseDown(object sender, MouseEventArgs e)
        {
            selectNode = this.treeViewLogicTree.GetNodeAt(e.X, e.Y);
        }

        /// <summary>
        /// 导入模型
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void importModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int groupId = int.Parse(nodekeyMap[selectNode.Text].ToString());

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "MDB File(*.mdb)|*.mdb|XML File(*.xml)|*.xml|OSG Files(*.osg)|*.osg";
            dlg.Multiselect = false;
            if(System.IO.Directory.Exists(strMediaPath))
            {
                dlg.InitialDirectory = strMediaPath + @"\mdb+osg";
            }
            dlg.RestoreDirectory = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Import importModel = null;
                    int index = dlg.FilterIndex;
                    string strFile = dlg.FileName;
                    int nCount = -1;
                    if (index == 1)	  //mdb
                    {
                        importModel = new ImportMDB();
                        //1、检查mdb格式是否正确
                        bool bRet = ((ImportMDB)importModel).CheckMDBFile(strFile, out nCount);
                        if (!bRet)
                            return;
                    }
                    else if (index == 2)	  //xml
                    {
                        importModel = new ImportXML();
                        //1、检查xml格式是否正确
                        nCount = ((ImportXML)importModel).GetCount(strFile);
                        if (nCount <= 0)
                            return;
                    }
                    else
                    {
                        importModel = new ImportOSG();
                        nCount = strFile.Length;
                        if (nCount <= 0)
                            return;
                    }

                    importModel.ImportModel(fc, strFile, groupId, nCount);                    
                }
                catch
                {
                    fc.LockType = gviLockType.gviLockSharedSchema;
                    fc.SetRenderIndexEnabled("Geometry", true);
                    this.axRenderControl1.ResumeLayout();
                }
            }
        }
    }
}
